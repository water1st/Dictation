using Dapper;
using Dictation.Core.Importing;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证独立复习会话的原子创建、归属、并发唯一和整组删除级联。</summary>
    public sealed class ReviewSessionDataAccessorTests
    {
        [Fact]
        public async Task Create_RoundTripsOrderedGroupsAndDistinctMemberForms()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var first = await environment.ImportAsync(decision: ImportDecision.Review);
            var second = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var request = Request(first) with
            {
                Groups = new[]
                {
                    new ReviewSessionGroupSeedPo(second, new[] { new ReviewSessionFormSeedPo("jmdict:2:1", "Dictionary") }),
                    new ReviewSessionGroupSeedPo(first, new[] { new ReviewSessionFormSeedPo("jmdict:1:1", "Te"), new ReviewSessionFormSeedPo("jmdict:1:1", "Dictionary") })
                }
            };
            var created = await Accessor(environment).CreateAsync(request, Token);
            Assert.Equal("session", created.SessionId);
            Assert.Equal(new[] { second, first }, created.WordGroupIds);
            var active = Assert.IsType<ReviewSessionPo>(await Accessor(environment).GetActiveAsync("Japanese", Token));
            Assert.Equal(request.PlanJson, active.PlanJson);
            Assert.Equal(request.CreatedAtUtc, active.CreatedAtUtc);
            Assert.Equal(new[] { second, first }, active.WordGroupIds);
            Assert.Equal(3L, await CountAsync(environment, "ReviewSessionForms"));
            var browse = await Accessor(environment).GetBrowseItemsAsync("session", Token);
            Assert.Equal(new[] { second, first }, browse.Select(group => group.WordGroupId));
            Assert.Contains("吃", Assert.Single(browse[1].Members).Meanings);
        }

        [Theory]
        [InlineData("Deleted")] [InlineData("Learning")] [InlineData("English")]
        [InlineData("WrongMember")] [InlineData("WrongForm")]
        public async Task Create_InvalidatedGroupOrMemberRejectsWholeTransaction(string scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var request = Request(group);
            await using (var connection = environment.Connections.CreateConnection())
            {
                switch (scenario)
                {
                    case "Deleted": await connection.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId=@Id;", new { Id = group }); break;
                    case "Learning": await connection.ExecuteAsync("UPDATE WordGroups SET Placement='Learning' WHERE WordGroupId=@Id;", new { Id = group }); break;
                    case "English": await connection.ExecuteAsync("UPDATE WordGroups SET Language='English' WHERE WordGroupId=@Id;", new { Id = group }); break;
                    case "WrongMember": request = request with { Groups = new[] { new ReviewSessionGroupSeedPo(group, new[] { new ReviewSessionFormSeedPo("missing", "Dictionary") }) } }; break;
                    default: request = request with { Groups = new[] { new ReviewSessionGroupSeedPo(group, new[] { new ReviewSessionFormSeedPo("jmdict:1:1", "Past") }) } }; break;
                }
            }
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Accessor(environment).CreateAsync(request, Token));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessions"));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessionGroups"));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessionForms"));
        }

        [Fact]
        public async Task Create_ChildFailureRollsBackEntireNewSession()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.ExecuteAsync("CREATE TRIGGER RejectReviewSessionForm BEFORE INSERT ON ReviewSessionForms BEGIN SELECT RAISE(ABORT,'测试中途失败'); END;");
            }
            await Assert.ThrowsAsync<SqliteException>(() => Accessor(environment).CreateAsync(Request(group), Token));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessions"));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessionGroups"));
        }

        [Fact]
        public async Task Create_ConcurrentRequestsReturnSameActivityWithoutReplacingSnapshot()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var requests = Enumerable.Range(0, 8).Select(index => Task.Run(() => Accessor(environment).CreateAsync(Request(group) with { SessionId = "session" + index }, Token)));
            var results = await Task.WhenAll(requests);
            Assert.Single(results.Select(session => session.SessionId).Distinct());
            Assert.All(results, session => Assert.Equal(new[] { group }, session.WordGroupIds));
            Assert.Equal(1L, await CountAsync(environment, "ReviewSessions"));
        }

        [Fact]
        public async Task Delete_AllGroupsCascadeAndDoNotBlockNewActivity()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var old = await environment.ImportAsync(decision: ImportDecision.Review);
            await Accessor(environment).CreateAsync(Request(old), Token);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId=@Id;", new { Id = old });
            }
            Assert.Null(await Accessor(environment).GetActiveAsync("Japanese", Token));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessionForms"));
            var next = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var created = await Accessor(environment).CreateAsync(Request(next) with
            {
                SessionId = "next", Groups = new[] { new ReviewSessionGroupSeedPo(next, new[] { new ReviewSessionFormSeedPo("jmdict:2:1", "Dictionary") }) }
            }, Token);
            Assert.Equal("next", created.SessionId);
            Assert.Equal(new[] { next }, created.WordGroupIds);
        }

        [Fact]
        public async Task GetActive_ResumesAcrossDateBoundaryWithoutRecreatingGroups()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var request = Request(group) with { CreatedAtUtc = MemorySeedTestEnvironment.Now.AddDays(-1), LocalDate = "2026-09-29" };
            await Accessor(environment).CreateAsync(request, Token);
            var resumed = await Accessor(environment).CreateAsync(Request(group) with { SessionId = "new" }, Token);
            Assert.Equal("session", resumed.SessionId);
            Assert.Equal(request.CreatedAtUtc, resumed.CreatedAtUtc);
            Assert.Equal(1L, await CountAsync(environment, "ReviewSessions"));
        }

        [Fact]
        public async Task CountCompleted_UsesDistinctGroupAndRequestedLanguageDate()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.ExecuteAsync("""
                    INSERT INTO ReviewSessions VALUES ('a','Japanese','Completed','{}',@Now,'2026-09-30',@Now);
                    INSERT INTO ReviewSessions VALUES ('b','Japanese','Completed','{}',@Now,'2026-09-30',@Now);
                    INSERT INTO ReviewSessionGroups VALUES ('a',@Id,0,'2026-09-30');
                    INSERT INTO ReviewSessionGroups VALUES ('b',@Id,0,'2026-09-30');
                    """, new { Now = MemorySeedTestEnvironment.Now.ToString("O"), Id = group });
            }
            Assert.Equal(1, await Accessor(environment).CountCompletedOnDateAsync("Japanese", "2026-09-30", Token));
            Assert.Equal(0, await Accessor(environment).CountCompletedOnDateAsync("English", "2026-09-30", Token));
            Assert.Equal(0, await Accessor(environment).CountCompletedOnDateAsync("Japanese", "2026-10-01", Token));
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
        public async Task Create_InvalidRequestRejectsBeforeAnyRows(int scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var request = scenario switch
            {
                0 => Request(group) with { Language = "Alien" },
                1 => Request(group) with { SessionId = " " },
                2 => Request(group) with { PlanJson = "not-json" },
                3 => Request(group) with { LocalDate = "not-date" },
                4 => Request(group) with { Groups = Array.Empty<ReviewSessionGroupSeedPo>() },
                5 => Request(group) with { Groups = null! },
                6 => Request(group) with { Groups = new[] { new ReviewSessionGroupSeedPo(group, Array.Empty<ReviewSessionFormSeedPo>()) } },
                _ => Request(group) with { Groups = new[] { Request(group).Groups[0], Request(group).Groups[0] } }
            };
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Accessor(environment).CreateAsync(request, Token));
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessions"));
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public async Task Read_InvalidLanguageEmptyIdUnknownSessionOrDateRejects(int scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            if (scenario == 2)
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(() => Accessor(environment).GetBrowseItemsAsync("missing", Token));
                return;
            }
            await Assert.ThrowsAnyAsync<ArgumentException>(() => scenario switch
            {
                0 => Accessor(environment).GetActiveAsync("Alien", Token),
                1 => (Task)Accessor(environment).GetBrowseItemsAsync(" ", Token),
                _ => Accessor(environment).CountCompletedOnDateAsync("Japanese", "wrong-date", Token)
            });
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public async Task Cancelled_OperationsPropagateWithoutRows(int operation)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
            {
                0 => Accessor(environment).GetActiveAsync("Japanese", cancellation.Token),
                1 => Accessor(environment).CreateAsync(Request(group), cancellation.Token),
                2 => (Task)Accessor(environment).GetBrowseItemsAsync("missing", cancellation.Token),
                _ => Accessor(environment).CountCompletedOnDateAsync("Japanese", "2026-09-30", cancellation.Token)
            });
            Assert.Equal(0L, await CountAsync(environment, "ReviewSessions"));
        }

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static ReviewSessionDataAccessor Accessor(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<ReviewSessionDataAccessor>();
        private static CreateReviewSessionPo Request(string group) => new("session", "Japanese", "{}", MemorySeedTestEnvironment.Now, "2026-09-30",
            new[] { new ReviewSessionGroupSeedPo(group, new[] { new ReviewSessionFormSeedPo("jmdict:1:1", "Dictionary") }) });
        private static async Task<long> CountAsync(MemorySeedTestEnvironment environment, string table)
        {
            await using var connection = environment.Connections.CreateConnection();
            return await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM " + table + ";");
        }
    }
}
