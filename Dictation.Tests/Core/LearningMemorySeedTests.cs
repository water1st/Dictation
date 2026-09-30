using Dapper;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Configuration;
using Dictation.Core.Review;
using Dictation.DataAccess.Learning;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>验证每个完成词形按会话首错保留客观评价，并与真实学习事务共同保存。</summary>
    public sealed class LearningMemorySeedTests
    {
        [Fact]
        public void Seeds_OneFormWrongKeepsAgainAfterRetryWithoutPenalizingOtherForm()
        {
            var planner = new LearningMemorySeedPlanner(new FsrsScheduler());
            var result = planner.CreateSeeds(new[] { Unit("Dictionary"), Unit("Te") },
                new[] { History("Dictionary", true), History("Te", false), History("Te", true), History("Te", true) },
                MemorySeedTestEnvironment.Now);
            Assert.Equal(2, result.Count);
            var good = Assert.Single(result, x => x.FormCode == "Dictionary");
            var again = Assert.Single(result, x => x.FormCode == "Te");
            Assert.Equal(2.3065, good.Stability);
            Assert.Equal(0, good.LapseCount);
            Assert.Equal(0.212, again.Stability);
            Assert.Equal(1, again.LapseCount);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(1), again.DueAtUtc);
        }

        [Fact]
        public void Seeds_UnansweredOrUnmasteredFormsDoNotPretendGood()
        {
            var result = new LearningMemorySeedPlanner(new FsrsScheduler()).CreateSeeds(
                new[] { Unit("Dictionary"), Unit("Te") with { IsMastered = false } },
                new[] { History("Dictionary", null), History("Te", true) }, MemorySeedTestEnvironment.Now);
            Assert.Empty(result);
        }

        [Fact]
        public void Seeds_OtherGroupOrMemberErrorDoesNotChangeCurrentForm()
        {
            var result = new LearningMemorySeedPlanner(new FsrsScheduler()).CreateSeeds(new[] { Unit("Dictionary") },
                new[] { History("Dictionary", true), History("Dictionary", false) with { MemberId = 22 },
                    History("Dictionary", false) with { WordGroupId = "another" } }, MemorySeedTestEnvironment.Now);
            Assert.Equal(0, Assert.Single(result).LapseCount);
        }

        [Fact]
        public void Seeds_RejectsNullInputButEmptyScopeIsValid()
        {
            var planner = new LearningMemorySeedPlanner(new FsrsScheduler());
            Assert.Throws<ArgumentNullException>(() => planner.CreateSeeds(null!, Array.Empty<LearningQuestionHistoryPo>(), MemorySeedTestEnvironment.Now));
            Assert.Throws<ArgumentNullException>(() => planner.CreateSeeds(Array.Empty<LearningFormUnitPo>(), null!, MemorySeedTestEnvironment.Now));
            Assert.Empty(planner.CreateSeeds(Array.Empty<LearningFormUnitPo>(), Array.Empty<LearningQuestionHistoryPo>(), MemorySeedTestEnvironment.Now));
        }

        [Fact]
        public async Task Learning_CompletionWritesSeparateGoodAndAgainMemories()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary", "Te");
            await environment.FinishAsync(errorForm: "Te");
            var rows = await environment.MemoriesAsync(group);
            Assert.Equal(2, rows.Count);
            Assert.Equal(2.3065, (double)rows.Single(x => (string)x.FormCode == "Dictionary").Stability);
            Assert.Equal(0.212, (double)rows.Single(x => (string)x.FormCode == "Te").Stability);
            Assert.Equal(1L, (long)rows.Single(x => (string)x.FormCode == "Te").LapseCount);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Review", await connection.ExecuteScalarAsync<string>("SELECT Placement FROM WordGroups WHERE WordGroupId = @GroupId;", new { GroupId = group }));
        }

        [Fact]
        public async Task Learning_SupplementalFormDoesNotResetExistingMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync("UPDATE ReviewFormMemories SET Stability = 100, ReviewCount = 7, LapseCount = 1, DueAtUtc = @Due WHERE WordGroupId = @GroupId;",
                    new { Due = MemorySeedTestEnvironment.Now.AddDays(100).ToString("O"), GroupId = group });
            }
            await environment.SavePlanAsync("Dictionary", "Te");
            await environment.FinishAsync();
            var rows = await environment.MemoriesAsync(group);
            Assert.Equal(2, rows.Count);
            var old = rows.Single(x => (string)x.FormCode == "Dictionary");
            Assert.Equal(100, (double)old.Stability);
            Assert.Equal(7L, (long)old.ReviewCount);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(100).ToString("O"), (string)old.DueAtUtc);
            Assert.Equal(1L, (long)rows.Single(x => (string)x.FormCode == "Te").ReviewCount);
        }

        [Fact]
        public async Task Learning_FailedMemoryInsertRollsBackAnswerProgressAndPlacementThenCanRetry()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            var session = await environment.Sessions.StartOrResumeAsync(new StartLearningSessionRequestDto(LearningLanguage.Japanese), TestContext.Current.CancellationToken);
            var first = Assert.IsType<LearningQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, TestContext.Current.CancellationToken));
            var next = Assert.IsType<LearningQuestionDto>((await environment.Sessions.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, first.QuestionId, await environment.CorrectAnswerAsync(first)), TestContext.Current.CancellationToken)).NextQuestion);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync("""
                    CREATE TRIGGER RejectMemory BEFORE INSERT ON ReviewFormMemories
                    BEGIN SELECT RAISE(ABORT, '测试记忆写入失败'); END;
                    """);
            }
            var answer = new SubmitLearningAnswerRequestDto(session.SessionId, next.QuestionId, await environment.CorrectAnswerAsync(next));
            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => environment.Sessions.SubmitAnswerAsync(answer, TestContext.Current.CancellationToken));
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
                Assert.Equal("Pending", await connection.ExecuteScalarAsync<string>("SELECT Status FROM LearningQuestions WHERE QuestionId = @Id;", new { Id = next.QuestionId }));
                Assert.Equal("Learning", await connection.ExecuteScalarAsync<string>("SELECT Placement FROM WordGroups WHERE WordGroupId = @GroupId;", new { GroupId = group }));
                await connection.ExecuteAsync("DROP TRIGGER RejectMemory;");
            }
            Assert.Empty(await environment.MemoriesAsync(group));
            Assert.NotNull((await environment.Sessions.SubmitAnswerAsync(answer, TestContext.Current.CancellationToken)).Summary);
            Assert.Single(await environment.MemoriesAsync(group));
        }

        [Fact]
        public async Task Import_DirectReviewDoesNotCreateFabricatedLearningMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        private static LearningFormUnitPo Unit(string form) => new("group", 11, form, form, false,
            "None", new[] { "吃" }, Array.Empty<LearningAnswerVariantPo>(), 2, true, 0, 2, 0, 0, 2, 0, 2, 0);
        private static LearningQuestionHistoryPo History(string form, bool? correct) => new(1, "group", 11, form, "MeaningWriting", "Kana", 0, correct);
    }
}
