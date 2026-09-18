using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证会话启动恢复、候选选择、计划读取与持久化协作。
    /// </summary>
    public sealed class LearningSessionServiceTests
    {
        [Fact]
        public async Task StartOrResumeAsync_ResumesSameActiveSessionAndKeepsLanguagesIndependent()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");

            var japanese = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            var resumed = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            var english = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.English),
                TestContext.Current.CancellationToken);

            Assert.Equal(japanese.SessionId, resumed.SessionId);
            Assert.Equal(japanese.Status, resumed.Status);
            Assert.Equal(japanese.WordGroupIds, resumed.WordGroupIds);
            Assert.NotEqual(japanese.SessionId, english.SessionId);
            Assert.Equal(LearningLanguage.Japanese, japanese.Language);
            Assert.Equal(LearningLanguage.English, english.Language);
            Assert.Single(japanese.WordGroupIds);
            Assert.Single(english.WordGroupIds);
        }

        [Fact]
        public async Task StartOrResumeAsync_SequentialUsesCreatedTimeThenGroupIdAndCountsWholeGroups()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var first = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            var second = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var third = await environment.StageLearningAsync(LearningLanguage.Japanese, "食事", "jmdict:10:1");
            await environment.SetCreatedAtAsync(first, "2026-09-05T03:00:00.0000000+00:00");
            await environment.SetCreatedAtAsync(second, "2026-09-05T01:00:00.0000000+00:00");
            await environment.SetCreatedAtAsync(third, "2026-09-05T02:00:00.0000000+00:00");
            await environment.SaveJapanesePlanAsync(LearningSelectionMode.Sequential, 3, 20, new[] { "Dictionary" });

            var session = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);

            Assert.Equal(new[] { second, third, first }, session.WordGroupIds);
            Assert.Equal(3, session.WordGroupIds.Count);
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningFormStates WHERE WordGroupId = @WordGroupId;",
                new { WordGroupId = first }));
        }

        [Fact]
        public async Task StartOrResumeAsync_RandomSelectionIsDeterministicWithInjectedSeed()
        {
            const int seed = 3821;
            using var environment = await TestEnvironment.CreateAsync(seed);
            var groupIds = new[]
            {
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食事", "jmdict:10:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "書く", "jmdict:4:1")
            };
            for (var index = 0; index < groupIds.Length; index++)
            {
                await environment.SetCreatedAtAsync(groupIds[index], $"2026-09-05T0{index + 1}:00:00.0000000+00:00");
            }
            await environment.SaveJapanesePlanAsync(LearningSelectionMode.Random, 3, 20, new[] { "Dictionary" });
            var expected = groupIds.ToArray();
            Shuffle(expected, new Random(seed));

            var session = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);

            Assert.Equal(expected.Take(3), session.WordGroupIds);
        }

        [Fact]
        public async Task StartOrResumeAsync_ManualKeepsOrderAndRejectsInvalidSelections()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var first = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var second = await environment.StageLearningAsync(LearningLanguage.Japanese, "食事", "jmdict:10:1");
            var review = await environment.StageAsync(LearningLanguage.Japanese, "書く", "jmdict:4:1", ImportDecision.Review);
            var english = await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");
            await environment.SaveJapanesePlanAsync(LearningSelectionMode.Manual, 20, 20, new[] { "Dictionary" });

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese, Array.Empty<string>()),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese, new[] { first, first }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese, new[] { review }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese, new[] { english }),
                TestContext.Current.CancellationToken));

            var session = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese, new[] { second, first }),
                TestContext.Current.CancellationToken);
            Assert.Equal(new[] { second, first }, session.WordGroupIds);
        }

        [Fact]
        public async Task StartOrResumeAsync_DailyNewQuotaDoesNotCarryAndUsesLocalDate()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var ids = new[]
            {
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "食事", "jmdict:10:1"),
                await environment.StageLearningAsync(LearningLanguage.Japanese, "書く", "jmdict:4:1")
            };
            await environment.SaveJapanesePlanAsync(LearningSelectionMode.Sequential, 5, 2, new[] { "Dictionary" });

            var first = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            Assert.Equal(2, first.WordGroupIds.Count);
            await environment.CompleteSessionAndMoveGroupsToReviewAsync(first);

            var sameDay = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            Assert.Equal(LearningSessionStatus.Completed, sameDay.Status);
            Assert.Empty(sameDay.WordGroupIds);

            environment.TimeProvider.Value = environment.TimeProvider.Value.AddDays(1);
            var nextDay = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            Assert.Equal(LearningSessionStatus.InProgress, nextDay.Status);
            Assert.Equal(2, nextDay.WordGroupIds.Count);
            Assert.All(nextDay.WordGroupIds, id => Assert.Contains(id, ids.Skip(2)));
        }

        [Fact]
        public async Task StartOrResumeAsync_PrioritizesHistoricalGroupAndAddsNewlyEnabledFormState()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var historical = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SaveJapanesePlanAsync(LearningSelectionMode.Sequential, 1, 20, new[] { "Dictionary" });
            var firstSession = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            await environment.CompleteSessionAsync(firstSession.SessionId);
            var fresh = await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            await environment.SaveJapanesePlanAsync(
                LearningSelectionMode.Sequential,
                1,
                20,
                new[] { "Dictionary", "Polite" });

            var resumedHistory = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);

            Assert.Equal(new[] { historical }, resumedHistory.WordGroupIds);
            Assert.DoesNotContain(fresh, resumedHistory.WordGroupIds);
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.True(await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningFormStates WHERE WordGroupId = @WordGroupId AND FormCode = 'Polite';",
                new { WordGroupId = historical }) > 0L);
        }

        [Fact]
        public async Task StartOrResumeAsync_EmptyPoolReturnsCompletedSessionWithoutThrowing()
        {
            using var environment = await TestEnvironment.CreateAsync();

            var session = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);

            Assert.Equal(LearningSessionStatus.Completed, session.Status);
            Assert.Empty(session.WordGroupIds);
            Assert.NotNull(session.CompletedAtUtc);
            Assert.Null(await environment.Service.GetActiveAsync(
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetBrowseItemsAsync_MapsChineseSnapshotWithoutChangingLearningState()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            var session = await environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            var before = await environment.ReadStateCountsAsync();

            var items = await environment.Service.GetBrowseItemsAsync(session.SessionId, TestContext.Current.CancellationToken);
            var after = await environment.ReadStateCountsAsync();

            var member = Assert.Single(Assert.Single(items).Members);
            Assert.Equal("食べる", member.Headword);
            Assert.Contains("吃", member.Meanings);
            Assert.Contains(member.Forms, form => form.FormCode == "Polite" && form.Surface == "食べます");
            Assert.Equal(before, after);
        }

        [Fact]
        public async Task StartOrResumeAsync_PreCanceledTokenDoesNotCreateSession()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => environment.Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                cancellation.Token));

            Assert.Null(await environment.Service.GetActiveAsync(
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken));
        }

        private static void Shuffle<T>(T[] values, Random random)
        {
            for (var index = values.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory;

            private TestEnvironment(
                TemporaryDirectory temporaryDirectory,
                ApplicationPaths paths,
                MutableTimeProvider timeProvider,
                LearningPlanService learningPlanService,
                ImportInboxService importService,
                LearningSessionService service)
            {
                this.temporaryDirectory = temporaryDirectory;
                Paths = paths;
                TimeProvider = timeProvider;
                LearningPlanService = learningPlanService;
                ImportService = importService;
                Service = service;
            }

            public ApplicationPaths Paths { get; }
            public MutableTimeProvider TimeProvider { get; }
            public LearningPlanService LearningPlanService { get; }
            public ImportInboxService ImportService { get; }
            public LearningSessionService Service { get; }

            public static async Task<TestEnvironment> CreateAsync(int randomSeed = 1234)
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
                var connectionFactory = new SqliteConnectionFactory(paths);
                var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 5, 6, 0, 0, TimeSpan.FromHours(8)));
                var planService = new LearningPlanService(
                    DataAccessorTestFactory.CreateLearningPlan(connectionFactory),
                    lexiconService,
                    timeProvider);
                var importService = new ImportInboxService(lexiconService, DataAccessorTestFactory.CreateImportInbox(connectionFactory));
                var random = new Random(randomSeed);
                var sessionService = new LearningSessionService(
                    DataAccessorTestFactory.CreateLearningSession(connectionFactory),
                    planService,
                    planService,
                    new LearningQuestionScheduler(timeProvider, random),
                    new AnswerEvaluationService(),
                    timeProvider,
                    random);
                return new TestEnvironment(temporaryDirectory, paths, timeProvider, planService, importService, sessionService);
            }

            public Task<string> StageLearningAsync(LearningLanguage language, string sourceText, string sourceSenseId) =>
                StageAsync(language, sourceText, sourceSenseId, ImportDecision.Learning);

            public async Task<string> StageAsync(
                LearningLanguage language,
                string sourceText,
                string sourceSenseId,
                ImportDecision decision)
            {
                var staged = await ImportService.StageAsync(
                    new StageImportBatchRequestDto(
                        language,
                        ImportSourceKind.Manual,
                        new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                    TestContext.Current.CancellationToken);
                var item = Assert.Single(staged.Batch.Items);
                await ImportService.DecideAsync(
                    new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, decision),
                    TestContext.Current.CancellationToken);
                return Assert.IsType<string>(item.WordGroupId);
            }

            public Task<LearningPlanDto> SaveJapanesePlanAsync(
                LearningSelectionMode selectionMode,
                int sessionLimit,
                int dailyTarget,
                IReadOnlyList<string> forms)
            {
                return LearningPlanService.SaveAsync(
                    new SaveLearningPlanRequestDto(
                        LearningLanguage.Japanese,
                        dailyTarget,
                        sessionLimit,
                        selectionMode,
                        EnglishFormLabelMode.GrammarName,
                        null,
                        forms,
                        new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                        new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }),
                    TestContext.Current.CancellationToken);
            }

            public async Task SetCreatedAtAsync(string groupId, string value)
            {
                await using var connection = new SqliteConnectionFactory(Paths).CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE WordGroups SET CreatedAtUtc = @Value WHERE WordGroupId = @WordGroupId;",
                    new { Value = value, WordGroupId = groupId });
            }

            public async Task CompleteSessionAsync(string sessionId)
            {
                await using var connection = new SqliteConnectionFactory(Paths).CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE LearningSessions SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc WHERE SessionId = @SessionId;",
                    new { CompletedAtUtc = TimeProvider.GetUtcNow().ToString("O"), SessionId = sessionId });
            }

            public async Task CompleteSessionAndMoveGroupsToReviewAsync(LearningSessionDto session)
            {
                await using var connection = new SqliteConnectionFactory(Paths).CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE LearningSessions SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc WHERE SessionId = @SessionId;",
                    new { CompletedAtUtc = TimeProvider.GetUtcNow().ToString("O"), session.SessionId },
                    transaction);
                await connection.ExecuteAsync(
                    "UPDATE WordGroups SET Placement = 'Review' WHERE WordGroupId IN @WordGroupIds;",
                    new { WordGroupIds = session.WordGroupIds },
                    transaction);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
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
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningFormStates;"),
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestions;"),
                    await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            }

            public void Dispose() => temporaryDirectory.Dispose();
        }

        public sealed class MutableTimeProvider : TimeProvider
        {
            public MutableTimeProvider(DateTimeOffset value)
            {
                Value = value;
            }

            public DateTimeOffset Value { get; set; }

            public override DateTimeOffset GetUtcNow() => Value.ToUniversalTime();

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
                "UTC+8",
                TimeSpan.FromHours(8),
                "UTC+8",
                "UTC+8");
        }
    }
}
