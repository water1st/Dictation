using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    /// <summary>
    /// 验证会话、候选与题目状态的数据库持久化和事务边界。
    /// </summary>
    public sealed class LearningSessionDataAccessorTests
    {
        [Fact]
        public async Task MigrateAsync_AddsLearningSessionSchemaAsVersionSix()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Equal(6L, await connection.ExecuteScalarAsync<long>("SELECT MAX(Version) FROM SchemaMigrations;"));
            var tables = (await connection.QueryAsync<string>(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'Learning%' ORDER BY name;"))
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "LearningFormStates", "LearningGroupProgress", "LearningPlanChannels", "LearningPlanForms",
                    "LearningPlanOutputs", "LearningPlans", "LearningQuestionAttempts", "LearningQuestions",
                    "LearningSessionGroups", "LearningSessions"
                },
                tables);
        }

        [Fact]
        public async Task CreateAsync_PersistsOrderedGroupsProgressAndAvailableFormStates()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var first = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            var second = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var accessor = environment.CreateAccessor();
            var request = await environment.CreateRequestAsync("session-jp", "Japanese", new[] { second, first });

            var created = await accessor.CreateAsync(request, TestContext.Current.CancellationToken);
            var active = await accessor.GetActiveAsync("Japanese", TestContext.Current.CancellationToken);

            Assert.Equal("session-jp", created.SessionId);
            Assert.Equal(new[] { second, first }, created.WordGroupIds);
            Assert.NotNull(active);
            Assert.Equal(created.SessionId, active.SessionId);
            Assert.Equal(created.Status, active.Status);
            Assert.Equal(created.WordGroupIds, active.WordGroupIds);
            Assert.Equal(2, await accessor.CountStartedOnDateAsync("Japanese", "2026-09-05", TestContext.Current.CancellationToken));
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessionGroups;"));
            Assert.True(await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningFormStates;") >= 2L);
            var candidates = await accessor.GetCandidatesAsync("Japanese", TestContext.Current.CancellationToken);
            Assert.All(candidates, candidate => Assert.True(candidate.HasProgress));
            Assert.All(candidates, candidate => Assert.NotEmpty(candidate.AvailableForms));
        }

        [Fact]
        public async Task GetDailyProgressCountsAsync_CountsGroupsByLanguageAndLocalDate()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var first = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            var second = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var english = await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");
            var accessor = environment.CreateAccessor();
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("daily-jp", "Japanese", new[] { first, second }),
                TestContext.Current.CancellationToken);
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("daily-en", "English", new[] { english }),
                TestContext.Current.CancellationToken);
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                "UPDATE LearningGroupProgress SET CompletedLocalDate = @Date WHERE WordGroupId IN @WordGroupIds;",
                new { Date = "2026-09-05", WordGroupIds = new[] { first, english } });
            await connection.ExecuteAsync(
                "UPDATE LearningGroupProgress SET CompletedLocalDate = @Date WHERE WordGroupId = @WordGroupId;",
                new { Date = "2026-09-06", WordGroupId = second });

            var japaneseToday = await accessor.GetDailyProgressCountsAsync(
                "Japanese", "2026-09-05", TestContext.Current.CancellationToken);
            var englishToday = await accessor.GetDailyProgressCountsAsync(
                "English", "2026-09-05", TestContext.Current.CancellationToken);
            var japaneseTomorrow = await accessor.GetDailyProgressCountsAsync(
                "Japanese", "2026-09-06", TestContext.Current.CancellationToken);

            Assert.Equal(2, japaneseToday.StartedGroupCount);
            Assert.Equal(1, japaneseToday.CompletedGroupCount);
            Assert.Equal(1, englishToday.StartedGroupCount);
            Assert.Equal(1, englishToday.CompletedGroupCount);
            Assert.Equal(0, japaneseTomorrow.StartedGroupCount);
            Assert.Equal(1, japaneseTomorrow.CompletedGroupCount);
            Assert.True(await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningFormStates WHERE WordGroupId = @WordGroupId;",
                new { WordGroupId = first }) >= 1);
        }

        [Fact]
        public async Task GetDailyProgressCountsAsync_MultipleAnswersStillCountOneCompletedGroup()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(
                LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var accessor = environment.CreateAccessor();
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("answer-count", "Japanese", new[] { groupId }),
                TestContext.Current.CancellationToken);
            var form = Assert.Single((await environment.ReadGroupSeedAsync(groupId)).Forms);
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                "UPDATE LearningGroupProgress SET CompletedLocalDate = '2026-09-05' WHERE WordGroupId = @WordGroupId;",
                new { WordGroupId = groupId });
            for (var ordinal = 0; ordinal < 3; ordinal++)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO LearningQuestions (
                        SessionId, WordGroupId, MemberId, FormCode, Channel, OutputMode,
                        QuestionOrdinal, Status, CreatedAtUtc)
                    VALUES ('answer-count', @WordGroupId, @MemberId, @FormCode,
                        'MeaningWriting', 'Kana', @Ordinal, 'Answered', '2026-09-05T01:00:00Z');
                    """,
                    new { WordGroupId = groupId, form.MemberId, form.FormCode, Ordinal = ordinal });
                var questionId = await connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                await connection.ExecuteAsync(
                    """
                    INSERT INTO LearningQuestionAttempts (QuestionId, SubmittedText, WasCorrect, AnsweredAtUtc)
                    VALUES (@QuestionId, 'test', 1, '2026-09-05T01:00:00Z');
                    """,
                    new { QuestionId = questionId });
            }

            var counts = await accessor.GetDailyProgressCountsAsync(
                "Japanese", "2026-09-05", TestContext.Current.CancellationToken);

            Assert.Equal(3L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(1, counts.StartedGroupCount);
            Assert.Equal(1, counts.CompletedGroupCount);
        }

        [Fact]
        public async Task GetDailyProgressCountsAsync_RejectsInvalidInputAndPropagatesCancellation()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var accessor = environment.CreateAccessor();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => accessor.GetDailyProgressCountsAsync(
                "Unknown", "2026-09-05", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => accessor.GetDailyProgressCountsAsync(
                "Japanese", " ", TestContext.Current.CancellationToken));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accessor.GetDailyProgressCountsAsync(
                "Japanese", "2026-09-05", cancellation.Token));
        }

        [Fact]
        public async Task GetDailyProgressCountsAsync_WhenSchemaMissing_WrapsDatabaseFailure()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var accessor = DataAccessorTestFactory.CreateLearningSession(new SqliteConnectionFactory(paths));

            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => accessor.GetDailyProgressCountsAsync(
                "Japanese", "2026-09-05", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task CreateAsync_EnforcesOneActiveSessionPerLanguageButKeepsLanguagesIndependent()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var japanese = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var english = await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");
            var accessor = environment.CreateAccessor();
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("session-jp", "Japanese", new[] { japanese }),
                TestContext.Current.CancellationToken);
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("session-en", "English", new[] { english }),
                TestContext.Current.CancellationToken);
            var duplicate = await environment.CreateRequestAsync("session-jp-2", "Japanese", new[] { japanese });

            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => accessor.CreateAsync(
                duplicate,
                TestContext.Current.CancellationToken));

            Assert.Equal("session-jp", (await accessor.GetActiveAsync("Japanese", TestContext.Current.CancellationToken))?.SessionId);
            Assert.Equal("session-en", (await accessor.GetActiveAsync("English", TestContext.Current.CancellationToken))?.SessionId);
        }

        [Fact]
        public async Task CreateAsync_InvalidGroupRollsBackTheWholeSession()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var valid = await environment.ReadGroupSeedAsync(groupId);
            var invalid = new LearningSessionGroupSeedPo(
                "missing",
                new[] { new LearningFormIdentityPo(long.MaxValue, "Dictionary") });
            var request = new CreateLearningSessionPo(
                "rollback",
                "Japanese",
                "InProgress",
                "2026-09-05",
                DateTimeOffset.Parse("2026-09-05T01:00:00Z"),
                null,
                new[] { valid, invalid });
            var accessor = environment.CreateAccessor();

            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => accessor.CreateAsync(
                request,
                TestContext.Current.CancellationToken));

            Assert.Null(await accessor.GetActiveAsync("Japanese", TestContext.Current.CancellationToken));
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessions;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningGroupProgress;"));
        }

        [Fact]
        public async Task GetBrowseItemsAsync_ReturnsOrderedFullSnapshotsWithoutChangingState()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var first = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            var second = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var accessor = environment.CreateAccessor();
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("browse", "Japanese", new[] { second, first }),
                TestContext.Current.CancellationToken);
            var before = await environment.ReadStateCountsAsync();

            var items = await accessor.GetBrowseItemsAsync("browse", TestContext.Current.CancellationToken);
            var repeated = await accessor.GetBrowseItemsAsync("browse", TestContext.Current.CancellationToken);
            var after = await environment.ReadStateCountsAsync();

            Assert.Equal(new[] { second, first }, items.Select(item => item.WordGroupId));
            Assert.Equal(new[] { 0, 1 }, items.Select(item => item.SortOrder));
            Assert.All(items, item =>
            {
                Assert.NotEmpty(item.Members);
                Assert.All(item.Members, member =>
                {
                    Assert.NotEmpty(member.Meanings);
                    Assert.NotEmpty(member.Forms);
                });
            });
            Assert.Equal(items.Select(item => item.WordGroupId), repeated.Select(item => item.WordGroupId));
            Assert.Equal(before, after);
        }

        [Fact]
        public async Task DeletingWordGroup_CascadesItsSessionMembershipProgressAndFormStates()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var accessor = environment.CreateAccessor();
            await accessor.CreateAsync(
                await environment.CreateRequestAsync("cascade", "Japanese", new[] { groupId }),
                TestContext.Current.CancellationToken);

            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId = @WordGroupId;", new { WordGroupId = groupId });

            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessionGroups;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningGroupProgress;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningFormStates;"));
        }

        [Fact]
        public async Task CreateAsync_PreCanceledTokenDoesNotWrite()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var request = await environment.CreateRequestAsync("cancel", "Japanese", new[] { groupId });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => environment.CreateAccessor().CreateAsync(
                request,
                cancellation.Token));

            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessions;"));
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory;

            private TestEnvironment(TemporaryDirectory temporaryDirectory, ApplicationPaths paths, ImportInboxService importService)
            {
                this.temporaryDirectory = temporaryDirectory;
                Paths = paths;
                ImportService = importService;
            }

            public ApplicationPaths Paths { get; }
            public ImportInboxService ImportService { get; }

            public static async Task<TestEnvironment> CreateAsync()
            {
                var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken);
                var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
                var lexiconService = new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory));
                return new TestEnvironment(
                    temporaryDirectory,
                    paths,
                    new ImportInboxService(lexiconService, DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths))));
            }

            public LearningSessionDataAccessor CreateAccessor() =>
                DataAccessorTestFactory.CreateLearningSession(new SqliteConnectionFactory(Paths));

            public async Task<string> StageLearningAsync(LearningLanguage language, string sourceText, string sourceSenseId)
            {
                var staged = await ImportService.StageAsync(
                    new StageImportBatchRequestDto(
                        language,
                        ImportSourceKind.Manual,
                        new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                    TestContext.Current.CancellationToken);
                var item = Assert.Single(staged.Batch.Items);
                await ImportService.DecideAsync(
                    new DecideImportBatchRequestDto(
                        staged.Batch.BatchId,
                        new[] { item.ItemId },
                        ImportDecision.Learning),
                    TestContext.Current.CancellationToken);
                return Assert.IsType<string>(item.WordGroupId);
            }

            public async Task<CreateLearningSessionPo> CreateRequestAsync(
                string sessionId,
                string language,
                IReadOnlyList<string> groupIds)
            {
                var groups = new List<LearningSessionGroupSeedPo>();
                foreach (var groupId in groupIds)
                {
                    groups.Add(await ReadGroupSeedAsync(groupId));
                }

                return new CreateLearningSessionPo(
                    sessionId,
                    language,
                    "InProgress",
                    "2026-09-05",
                    DateTimeOffset.Parse("2026-09-05T01:00:00Z"),
                    null,
                    groups);
            }

            public async Task<LearningSessionGroupSeedPo> ReadGroupSeedAsync(string groupId)
            {
                await using var connection = new SqliteConnectionFactory(Paths).CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                var forms = (await connection.QueryAsync<LearningFormIdentityPo>(
                    """
                    SELECT DISTINCT WordGroupMembers.MemberId, WordGroupForms.FormCode
                    FROM WordGroupMembers
                    INNER JOIN WordGroupForms ON WordGroupForms.MemberId = WordGroupMembers.MemberId
                    WHERE WordGroupMembers.WordGroupId = @WordGroupId
                      AND WordGroupForms.FormCode IN ('Dictionary', 'Base')
                    ORDER BY WordGroupMembers.MemberId, WordGroupForms.FormCode;
                    """,
                    new { WordGroupId = groupId })).ToArray();
                return new LearningSessionGroupSeedPo(groupId, forms);
            }

            public async Task<string> ReadStateCountsAsync()
            {
                await using var connection = new SqliteConnectionFactory(Paths).CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                return string.Join(
                    "|",
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessions;"),
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningSessionGroups;"),
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningGroupProgress;"),
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningFormStates;"));
            }

            public void Dispose() => temporaryDirectory.Dispose();
        }
    }
}
