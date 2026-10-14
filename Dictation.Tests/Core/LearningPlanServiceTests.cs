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
    /// 验证学习计划保存、语言约束、学习库分页与会话启动选择。
    /// </summary>
    public sealed class LearningPlanServiceTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 9, 5, 2, 3, 4, TimeSpan.Zero);

        [Fact]
        public async Task LoadAsync_ReturnsIndependentLanguageDefaults()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            var japanese = await environment.Service.LoadAsync(
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken);
            var english = await environment.Service.LoadAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, japanese.Language);
            Assert.Equal(20, japanese.DailyNewTarget);
            Assert.Equal(20, japanese.SessionWordLimit);
            Assert.Equal(LearningSelectionMode.Random, japanese.SelectionMode);
            Assert.Equal(new[] { "Dictionary" }, japanese.FormCodes);
            Assert.Equal(new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation }, japanese.Channels);
            Assert.Equal(new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }, japanese.OutputModes);
            Assert.Equal(LearningLanguage.English, english.Language);
            Assert.Equal(new[] { "Base" }, english.FormCodes);
            Assert.Equal(new[] { LearningOutputMode.Latin }, english.OutputModes);
            Assert.Equal(EnglishFormLabelMode.GrammarName, english.EnglishFormLabelMode);
            Assert.Null(english.EnglishExampleSourceSenseId);
            Assert.Null(english.EnglishExampleParadigm);
        }

        [Fact]
        public async Task SaveAsync_RoundTripsLanguagePlanWithoutChangingOtherLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            var request = CreateJapaneseRequest(
                dailyTarget: 35,
                sessionLimit: 12,
                selectionMode: LearningSelectionMode.Sequential,
                formCodes: new[] { "Dictionary", "Polite", "Te" },
                channels: new[] { LearningChannel.MeaningWriting },
                outputs: new[] { LearningOutputMode.Kanji });

            var saved = await environment.Service.SaveAsync(request, TestContext.Current.CancellationToken);
            var reloaded = await environment.Service.LoadAsync(
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken);
            var english = await environment.Service.LoadAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);

            Assert.Equal(FixedNow, saved.UpdatedAtUtc);
            Assert.Equal(saved.Language, reloaded.Language);
            Assert.Equal(saved.DailyNewTarget, reloaded.DailyNewTarget);
            Assert.Equal(saved.SessionWordLimit, reloaded.SessionWordLimit);
            Assert.Equal(saved.SelectionMode, reloaded.SelectionMode);
            Assert.Equal(saved.FormCodes, reloaded.FormCodes);
            Assert.Equal(saved.Channels, reloaded.Channels);
            Assert.Equal(saved.OutputModes, reloaded.OutputModes);
            Assert.Equal(20, english.DailyNewTarget);
            Assert.Equal(new[] { "Base" }, english.FormCodes);
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(501, 20)]
        [InlineData(20, 0)]
        [InlineData(20, 501)]
        public async Task SaveAsync_RejectsDailyAndSessionCountsOutsideOneToFiveHundred(
            int dailyTarget,
            int sessionLimit)
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(dailyTarget: dailyTarget, sessionLimit: sessionLimit),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_RequiresAtLeastOneFormAndOneQuestionChannel()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(formCodes: Array.Empty<string>()),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(channels: Array.Empty<LearningChannel>()),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_JapaneseRequiresKanaOrKanjiAndRejectsLatin()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(outputs: Array.Empty<LearningOutputMode>()),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(outputs: new[] { LearningOutputMode.Latin }),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_EnglishRequiresOnlyLatinAndEnglishFormCodes()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(outputs: new[] { LearningOutputMode.Kana }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(outputs: new[] { LearningOutputMode.Latin, LearningOutputMode.Kanji }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(formCodes: new[] { "Dictionary" }),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_JapaneseRejectsEnglishFormsAndEnglishOptions()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(formCodes: new[] { "Past" }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateJapaneseRequest(
                    englishFormLabelMode: EnglishFormLabelMode.ExampleParadigm,
                    englishExampleSourceSenseId: "go%2:01:00::"),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_ExampleParadigmResolvesRealVerbAndAllEnabledForms()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            var enabledForms = new[] { "Base", "ThirdPersonSingular", "Past", "PastParticiple", "PresentParticiple" };

            var saved = await environment.Service.SaveAsync(
                CreateEnglishRequest(
                    formCodes: enabledForms,
                    labelMode: EnglishFormLabelMode.ExampleParadigm,
                    exampleSourceSenseId: "go%2:01:00::"),
                TestContext.Current.CancellationToken);

            Assert.Equal("go%2:01:00::", saved.EnglishExampleSourceSenseId);
            var paradigm = Assert.IsType<EnglishExampleParadigmDto>(saved.EnglishExampleParadigm);
            Assert.Equal("go", paradigm.Headword);
            Assert.Equal(enabledForms, paradigm.Forms.Select(form => form.FormCode));
            Assert.Equal(new[] { "go", "goes", "went", "gone", "going" }, paradigm.Forms.Select(form => form.Surface));
        }

        [Fact]
        public async Task SaveAsync_ExampleParadigmRejectsMissingNonVerbAndIncompleteVerb()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            var forms = new[] { "Base", "Past" };

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(
                    formCodes: forms,
                    labelMode: EnglishFormLabelMode.ExampleParadigm,
                    exampleSourceSenseId: "missing"),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(
                    formCodes: forms,
                    labelMode: EnglishFormLabelMode.ExampleParadigm,
                    exampleSourceSenseId: "conversion%1:04:00::"),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(
                    formCodes: forms,
                    labelMode: EnglishFormLabelMode.ExampleParadigm,
                    exampleSourceSenseId: "take_off%2:38:00::"),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_InvalidExampleParadigmDoesNotPersistPlan()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.SaveAsync(
                CreateEnglishRequest(
                    formCodes: new[] { "Base", "Past" },
                    labelMode: EnglishFormLabelMode.ExampleParadigm,
                    exampleSourceSenseId: "missing"),
                TestContext.Current.CancellationToken));

            var loaded = await environment.Service.LoadAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);
            Assert.Equal(EnglishFormLabelMode.GrammarName, loaded.EnglishFormLabelMode);
            Assert.Null(loaded.EnglishExampleSourceSenseId);
        }

        [Fact]
        public async Task CreateStartRequestAsync_ManualModeRequiresDistinctExistingLearningGroupIds()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            await environment.Service.SaveAsync(
                CreateJapaneseRequest(selectionMode: LearningSelectionMode.Manual),
                TestContext.Current.CancellationToken);
            var groupId = await StageAndDecideAsync(
                environment.ImportService,
                LearningLanguage.Japanese,
                "食前",
                "jmdict:2:1",
                ImportDecision.Learning);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                Array.Empty<string>(),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                new[] { groupId, groupId },
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                new[] { groupId, "missing" },
                TestContext.Current.CancellationToken));

            var result = await environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                new[] { groupId },
                TestContext.Current.CancellationToken);
            Assert.Equal(LearningLanguage.Japanese, result.Language);
            Assert.Equal(LearningSelectionMode.Manual, result.SelectionMode);
            Assert.Equal(new[] { groupId }, result.SelectedWordGroupIds);
        }

        [Fact]
        public async Task CreateStartRequestAsync_ManualModeRejectsGroupFromOtherLanguageOrPlacement()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            await environment.Service.SaveAsync(
                CreateJapaneseRequest(selectionMode: LearningSelectionMode.Manual),
                TestContext.Current.CancellationToken);
            var reviewId = await StageAndDecideAsync(
                environment.ImportService,
                LearningLanguage.Japanese,
                "食前",
                "jmdict:2:1",
                ImportDecision.Review);
            var englishId = await StageAndDecideAsync(
                environment.ImportService,
                LearningLanguage.English,
                "gone",
                "go%2:01:00::",
                ImportDecision.Learning);

            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                new[] { reviewId },
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Service.CreateStartRequestAsync(
                LearningLanguage.Japanese,
                new[] { englishId },
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetLibraryPageAsync_ValidatesPageAndMapsFullSnapshot()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            await StageAndDecideAsync(
                environment.ImportService,
                LearningLanguage.Japanese,
                "食べる",
                "jmdict:1:1",
                ImportDecision.Learning);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Service.GetLibraryPageAsync(
                LearningLanguage.Japanese,
                0,
                20,
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Service.GetLibraryPageAsync(
                LearningLanguage.Japanese,
                1,
                0,
                TestContext.Current.CancellationToken));

            var page = await environment.Service.GetLibraryPageAsync(
                LearningLanguage.Japanese,
                1,
                20,
                TestContext.Current.CancellationToken);
            var group = Assert.Single(page.Groups);
            var member = Assert.Single(group.Members);
            Assert.Equal("jmdict:1:1", member.SourceSenseId);
            Assert.Contains(member.Forms, form => form.FormCode == "Polite" && form.Surface == "食べます");
        }

        [Fact]
        public async Task DeleteWordGroupAsync_MapsDataAccessOutcomesAndRemovesWholeGroup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            var groupId = await StageAndDecideAsync(
                environment.ImportService,
                LearningLanguage.Japanese,
                "食前",
                "jmdict:2:1",
                ImportDecision.Learning);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => environment.Service.DeleteWordGroupAsync(
                LearningLanguage.Japanese,
                "missing",
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.DeleteWordGroupAsync(
                LearningLanguage.English,
                groupId,
                TestContext.Current.CancellationToken));

            await environment.Service.DeleteWordGroupAsync(
                LearningLanguage.Japanese,
                groupId,
                TestContext.Current.CancellationToken);
            var page = await environment.Service.GetLibraryPageAsync(
                LearningLanguage.Japanese,
                1,
                20,
                TestContext.Current.CancellationToken);
            Assert.Empty(page.Groups);
        }

        [Fact]
        public async Task LoadAsync_PreCanceledTokenPropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var environment = await CreateEnvironmentAsync(temporaryDirectory);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => environment.Service.LoadAsync(
                LearningLanguage.Japanese,
                cancellation.Token));
        }

        private static SaveLearningPlanRequestDto CreateJapaneseRequest(
            int dailyTarget = 20,
            int sessionLimit = 20,
            LearningSelectionMode selectionMode = LearningSelectionMode.Random,
            IReadOnlyList<string>? formCodes = null,
            IReadOnlyList<LearningChannel>? channels = null,
            IReadOnlyList<LearningOutputMode>? outputs = null,
            EnglishFormLabelMode englishFormLabelMode = EnglishFormLabelMode.GrammarName,
            string? englishExampleSourceSenseId = null)
        {
            return new SaveLearningPlanRequestDto(
                LearningLanguage.Japanese,
                dailyTarget,
                sessionLimit,
                selectionMode,
                englishFormLabelMode,
                englishExampleSourceSenseId,
                formCodes ?? new[] { "Dictionary" },
                channels ?? new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                outputs ?? new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji });
        }

        private static SaveLearningPlanRequestDto CreateEnglishRequest(
            IReadOnlyList<string>? formCodes = null,
            IReadOnlyList<LearningOutputMode>? outputs = null,
            EnglishFormLabelMode labelMode = EnglishFormLabelMode.GrammarName,
            string? exampleSourceSenseId = null)
        {
            return new SaveLearningPlanRequestDto(
                LearningLanguage.English,
                20,
                20,
                LearningSelectionMode.Random,
                labelMode,
                exampleSourceSenseId,
                formCodes ?? new[] { "Base" },
                new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                outputs ?? new[] { LearningOutputMode.Latin });
        }

        private static async Task<TestEnvironment> CreateEnvironmentAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                TestContext.Current.CancellationToken);
            var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
            var lexiconService = new LexiconLookupService(
                new JapaneseLexiconDataAccessor(lexiconFactory),
                new EnglishLexiconDataAccessor(lexiconFactory));
            var inboxAccessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var importService = new ImportInboxService(lexiconService, inboxAccessor);
            var service = new LearningPlanService(
                DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths)),
                new PracticePlanOptionValidator(),
                new EnglishExampleParadigmResolver(lexiconService),
                new FixedTimeProvider(FixedNow));
            return new TestEnvironment(service, importService);
        }

        private static async Task<string> StageAndDecideAsync(
            ImportInboxService service,
            LearningLanguage language,
            string sourceText,
            string sourceSenseId,
            ImportDecision decision)
        {
            var staged = await service.StageAsync(
                new StageImportBatchRequestDto(
                    language,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                TestContext.Current.CancellationToken);
            var item = Assert.Single(staged.Batch.Items);
            await service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, decision),
                TestContext.Current.CancellationToken);
            return Assert.IsType<string>(item.WordGroupId);
        }

        private sealed record TestEnvironment(
            LearningPlanService Service,
            ImportInboxService ImportService);

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset value;

            public FixedTimeProvider(DateTimeOffset value)
            {
                this.value = value;
            }

            public override DateTimeOffset GetUtcNow()
            {
                return value;
            }
        }
    }
}
