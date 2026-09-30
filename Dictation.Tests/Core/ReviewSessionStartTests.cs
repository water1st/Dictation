using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实三层和 SQLite 验证风险选词、独立额度与可恢复计划快照。</summary>
    public sealed class ReviewSessionStartTests
    {
        [Fact]
        public async Task Start_UntestedWholeGroupCountsOnceAndKeepsFourFormsNotEightScripts()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SavePlanAsync(environment, "Dictionary", "Polite", "Te", "Ta");
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
            Assert.Equal(new[] { group }, session.WordGroupIds);
            Assert.Equal(4L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessionForms;"));
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        [Fact]
        public async Task Start_SelectsOnlyEnabledWeakDueFormNotStrongOrDisabledForms()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SavePlanAsync(environment, "Dictionary", "Te");
            await SeedAsync(environment, group, "Dictionary", dueDays: 10);
            await SeedAsync(environment, group, "Te", dueDays: -1);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(new[] { group }, session.WordGroupIds);
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(new[] { "Te" }, await connection.QueryAsync<string>("SELECT FormCode FROM ReviewSessionForms;"));
            Assert.Equal(2, (await environment.MemoriesAsync(group)).Count);
        }

        [Fact]
        public async Task Start_RiskPriorityAndSelectedScopeApplyBeforeSessionLimit()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var weak = await environment.ImportAsync(decision: ImportDecision.Review);
            var pending = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var excluded = await environment.ImportAsync("食事", "jmdict:10:1", ImportDecision.Review);
            await SeedAsync(environment, weak, "Dictionary", -1);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with
            {
                RangeMode = ReviewRangeMode.Selected, WordGroupIds = new[] { weak, pending }, SessionWordLimit = 2
            }, Token);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(new[] { pending, weak }, session.WordGroupIds);
            Assert.DoesNotContain(excluded, session.WordGroupIds);
        }

        [Fact]
        public async Task Start_DailyRemainingCountsDifferentGroupsNotForms()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var completed = await environment.ImportAsync(decision: ImportDecision.Review);
            await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            await environment.ImportAsync("食事", "jmdict:10:1", ImportDecision.Review);
            await SeedAsync(environment, completed, "Dictionary", 10);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with { DailyReviewTarget = 2, SessionWordLimit = 20 }, Token);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.ExecuteAsync("""
                    INSERT INTO ReviewSessions VALUES ('past', 'Japanese', 'Completed', '{}', @Now, '2026-09-29', @Now);
                    INSERT INTO ReviewSessionGroups VALUES ('past', @Group, 0, '2026-09-30');
                    """, new { Now = MemorySeedTestEnvironment.Now.ToString("O"), Group = completed });
            }
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Single(session.WordGroupIds);
            Assert.DoesNotContain(completed, session.WordGroupIds);
        }

        [Fact]
        public async Task Start_RareKanjiOnlyHasNoEligibleFormsAndCreatesNoEmptySession()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync("跪く", "jmdict:6:1", ImportDecision.Review);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with { OutputModes = new[] { LearningOutputMode.Kanji } }, Token);
            Assert.Null(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessions;"));
        }

        [Fact]
        public async Task Start_FutureEnabledMemoryAndDisabledUntestedFormsDoNotForceReview()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SeedAsync(environment, group, "Dictionary", 10);
            Assert.Null(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessions;"));
        }

        [Fact]
        public async Task Start_EnglishDerivedLexemesStayOneGroupWithTheirOwnForms()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await ImportEnglishAsync(environment);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with
            {
                Language = LearningLanguage.English, FormCodes = new[] { "Base", "Past" }, OutputModes = new[] { LearningOutputMode.Latin }
            }, Token);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.English, Token));
            Assert.Equal(LearningLanguage.English, session.Language);
            Assert.Equal(new[] { group }, session.WordGroupIds);
            Assert.Equal(4L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessionForms;"));
            var browse = Assert.Single(await Reader(environment).GetBrowseItemsAsync(session.SessionId, Token));
            Assert.Equal(new[] { "convert", "go" }, browse.Members.Select(member => member.Headword).Order());
        }

        [Fact]
        public async Task Start_ReopenAndNewProviderRestoreSamePlanInsteadOfApplyingEditedSettings()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
            await SavePlanAsync(environment, "Te");
            var resumed = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(session.SessionId, resumed.SessionId);
            Assert.Equal(new[] { "Dictionary" }, resumed.PlanSnapshot.FormCodes);
            var paths = environment.Provider.GetRequiredService<ApplicationPaths>();
            using var restarted = new ServiceCollection().AddDictationDataAccess(paths.Root).AddDictationCore().BuildServiceProvider();
            var reloaded = Assert.IsType<ReviewSessionDto>(await restarted.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(session.SessionId, reloaded.SessionId);
            Assert.Equal(session.WordGroupIds, reloaded.WordGroupIds);
            Assert.Equal(new[] { "Dictionary" }, reloaded.PlanSnapshot.FormCodes);
        }

        [Fact]
        public async Task Browse_ChineseMetadataDoesNotSeedMemoryOrChangeLearning()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var browse = Assert.Single(await Reader(environment).GetBrowseItemsAsync(session.SessionId, Token));
            Assert.Equal(group, browse.WordGroupId);
            var member = Assert.Single(browse.Members);
            Assert.Contains("吃", member.Meanings);
            Assert.Contains("二类", member.AttributeLabel);
            Assert.Contains(member.Forms, form => form.FormCode == "Te");
            Assert.Empty(await environment.MemoriesAsync(group));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM LearningGroupProgress;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM LearningSessions;"));
        }

        [Fact]
        public async Task Start_TwoLanguagesHaveSeparateActivitiesAndCapacity()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            await ImportEnglishAsync(environment);
            var japanese = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var english = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.English, Token));
            Assert.NotEqual(japanese.SessionId, english.SessionId);
            Assert.Equal(2L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessions WHERE Status='InProgress';"));
        }

        [Fact]
        public async Task Start_ConcurrentCallsReturnOnePersistedSession()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token))));
            Assert.All(results, session => Assert.IsType<ReviewSessionDto>(session));
            Assert.Single(results.Select(session => session!.SessionId).Distinct());
            Assert.Equal(1L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessions;"));
        }

        [Fact]
        public async Task GetActive_EmptyIsNullAndUnknownBrowseDoesNotInventSession()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            Assert.Null(await Reader(environment).GetActiveAsync(LearningLanguage.Japanese, Token));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Reader(environment).GetBrowseItemsAsync("missing", Token));
        }

        [Theory]
        [InlineData(-1, false)] [InlineData(99, false)] [InlineData(-1, true)] [InlineData(99, true)]
        public async Task Language_UnknownRejectsReadAndStart(int language, bool read)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => read
                ? Reader(environment).GetActiveAsync((LearningLanguage)language, Token)
                : Starter(environment).StartOrResumeAsync((LearningLanguage)language, Token));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData(" ")]
        public async Task Browse_EmptyIdIsRejected(string? id)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Reader(environment).GetBrowseItemsAsync(id!, Token));
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)]
        public async Task Cancelled_OperationsPropagateBeforeWriting(int operation)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
            {
                0 => Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, cancellation.Token),
                1 => Reader(environment).GetActiveAsync(LearningLanguage.Japanese, cancellation.Token),
                _ => (Task)Reader(environment).GetBrowseItemsAsync("missing", cancellation.Token)
            });
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessions;"));
        }

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static IReviewSessionStarter Starter(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewSessionStarter>();
        private static IReviewSessionReader Reader(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewSessionReader>();
        private static SaveReviewPlanRequestDto Plan() => new(LearningLanguage.Japanese, 20, 20, ReviewRangeMode.All, Array.Empty<string>(),
            new[] { "Dictionary" }, new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
            new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }, EnglishFormLabelMode.GrammarName, null);
        private static Task<ReviewPlanDto> SavePlanAsync(MemorySeedTestEnvironment environment, params string[] forms) => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with { FormCodes = forms }, Token);

        private static async Task<long> ScalarAsync(MemorySeedTestEnvironment environment, string sql)
        {
            await using var connection = environment.Connections.CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql);
        }

        private static async Task SeedAsync(MemorySeedTestEnvironment environment, string group, string form, int dueDays)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.ExecuteAsync("""
                INSERT INTO ReviewFormMemories
                SELECT @Group, MemberId, @Form, 1, 5, @Last, @Due, 1, 0 FROM WordGroupMembers WHERE WordGroupId=@Group;
                """, new { Group = group, Form = form, Last = MemorySeedTestEnvironment.Now.AddDays(-5).ToString("O"), Due = MemorySeedTestEnvironment.Now.AddDays(dueDays).ToString("O") });
        }

        private static async Task<string> ImportEnglishAsync(MemorySeedTestEnvironment environment)
        {
            var stage = await environment.Provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.English, ImportSourceKind.Manual, new[] { new ImportSelectionDto("go", "go%2:01:00::") }), Token);
            var item = Assert.Single(stage.Batch.Items);
            await environment.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(stage.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Review), Token);
            return item.WordGroupId!;
        }
    }
}
