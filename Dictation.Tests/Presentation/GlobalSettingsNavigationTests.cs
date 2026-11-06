using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Practice;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证全局设置入口保留业务页面上下文，并正确复用当前设置页。</summary>
    public sealed class GlobalSettingsNavigationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData("Lookup", LearningLanguage.Japanese)]
        [InlineData("Lookup", LearningLanguage.English)]
        [InlineData("LearningPlan", LearningLanguage.Japanese)]
        [InlineData("LearningPlan", LearningLanguage.English)]
        [InlineData("ReviewPlan", LearningLanguage.Japanese)]
        [InlineData("ReviewPlan", LearningLanguage.English)]
        [InlineData("ReviewLibrary", LearningLanguage.Japanese)]
        [InlineData("TextImport", LearningLanguage.English)]
        [InlineData("Inbox", LearningLanguage.English)]
        [InlineData("WrongAnswers", LearningLanguage.Japanese)]
        [InlineData("Statistics", LearningLanguage.English)]
        [InlineData("KanaPlan", LearningLanguage.Japanese)]
        [InlineData("KanaSession", LearningLanguage.Japanese)]
        [InlineData("LearningSession", LearningLanguage.English)]
        [InlineData("ReviewSession", LearningLanguage.Japanese)]
        [InlineData("Mcp", LearningLanguage.Japanese)]
        public async Task BusinessPage_SettingsRoundTrip_RestoresSamePageAndLanguage(string page, LearningLanguage language)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var shell = provider.GetRequiredService<ShellViewModel>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            await root.SelectLanguageAsync(language, Token);
            nav.CompleteTransition();
            await OpenPageAsync(nav, page, language);
            nav.CompleteTransition();
            var originalPage = nav.CurrentPage;
            var title = nav.PageTitle;
            Assert.True(shell.IsSettingsButtonVisible);

            await nav.OpenSettingsAsync(Token);
            var settings = Assert.IsType<SettingsViewModel>(nav.CurrentPage);
            Assert.Equal(language, settings.SettingsTargetLanguage);
            Assert.True(shell.IsSettingsButtonVisible);
            Assert.False(nav.CanGoBack);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            Assert.Same(originalPage, nav.CurrentPage);
            Assert.Equal(title, nav.PageTitle);
            nav.CompleteTransition();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => settings.Mcp.RefreshAsync(Token));

            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            if (page == "KanaSession")
            {
                Assert.IsType<KanaPlanViewModel>(nav.CurrentPage);
                await nav.GoBackAsync(Token);
                nav.CompleteTransition();
            }
            Assert.Same(root.Home, nav.CurrentPage);
        }

        [Theory]
        [InlineData(SettingsSection.Import)]
        [InlineData(SettingsSection.LearningReview)]
        [InlineData(SettingsSection.Mcp)]
        public async Task Settings_RepeatedEntry_SelectsAppearanceWithoutPushingHistory(SettingsSection section)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            await nav.OpenSettingsAsync(Token);
            nav.CompleteTransition();
            var settings = Assert.IsType<SettingsViewModel>(nav.CurrentPage);
            await settings.SelectTargetLanguageAsync(LearningLanguage.English);
            await settings.SelectSectionAsync(section, Token);
            var transitions = 0;
            nav.TransitionRequested += (_, _) => transitions++;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => nav.OpenSettingsAsync(new CancellationToken(true)));
            Assert.Equal(section, settings.SelectedSection);
            for (var i = 0; i < 3; i++) await nav.OpenSettingsAsync(Token);
            Assert.Same(settings, nav.CurrentPage);
            Assert.Equal(SettingsSection.Appearance, settings.SelectedSection);
            Assert.Equal(LearningLanguage.English, settings.SettingsTargetLanguage);
            Assert.Equal(0, transitions);
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(root, nav.CurrentPage);
            Assert.False(nav.CanGoBack);
        }

        [Fact]
        public async Task LearningPlan_SettingsRoundTrip_PreservesUnsavedInputsAndBusinessLanguage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            await nav.OpenLearningPlanAsync(LearningLanguage.English, Token);
            nav.CompleteTransition();
            var plan = Assert.IsType<LearningPlanViewModel>(nav.CurrentPage);
            plan.DailyNewTarget = 37;
            plan.SessionWordLimit = 13;
            plan.ExampleQuery = "unsaved";
            plan.FormOptions[0].IsSelected = !plan.FormOptions[0].IsSelected;
            var selected = plan.FormOptions[0].IsSelected;

            await nav.OpenSettingsAsync(Token);
            Assert.Equal(LearningLanguage.English, Assert.IsType<SettingsViewModel>(nav.CurrentPage).SettingsTargetLanguage);
            Assert.Equal(LearningLanguage.Japanese, root.Home.SelectedLanguage);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(plan, nav.CurrentPage);
            Assert.Equal(37, plan.DailyNewTarget);
            Assert.Equal(13, plan.SessionWordLimit);
            Assert.Equal("unsaved", plan.ExampleQuery);
            Assert.Equal(selected, plan.FormOptions[0].IsSelected);
        }

        [Fact]
        public async Task Lookup_SettingsRoundTrip_PreservesQueryResultAndScrollPosition()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            await nav.OpenLexiconLookupAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            var lookup = Assert.IsType<LexiconLookupViewModel>(nav.CurrentPage);
            lookup.Query = "食べる";
            await lookup.SearchAsync(Token);
            Assert.Equal(2, lookup.Results.Count);
            var result = lookup.Results[1];
            lookup.SelectedResult = result;
            lookup.ScrollOffset = 120;

            await nav.OpenSettingsAsync(Token);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(lookup, nav.CurrentPage);
            Assert.Equal("食べる", lookup.Query);
            Assert.Same(result, lookup.SelectedResult);
            Assert.Equal(120, lookup.ScrollOffset);
        }

        [Fact]
        public async Task Workspace_SettingsChangesPlan_RefreshesDailyProgressOnReturn()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            await root.SelectLanguageAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            await nav.OpenSettingsAsync(Token);
            var settings = nav.CurrentPage;
            nav.CompleteTransition();
            await nav.OpenLearningPlanAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            var plan = Assert.IsType<LearningPlanViewModel>(nav.CurrentPage);
            plan.DailyNewTarget = 37;
            await plan.SaveAsync(Token);
            Assert.Null(plan.ErrorMessage);
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(settings, nav.CurrentPage);
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(root.Home, nav.CurrentPage);
            Assert.EndsWith("/37", root.Home.LearningProgressLabel);
        }

        private static Task<ServiceProvider> CreateProviderAsync(string path)
        {
            return WorkspaceSettingsNavigationTests.CreateProviderAsync(path, services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
            });
        }

        [Fact]
        public async Task LearningSession_SettingsDuringSpeechPreparation_CancelsAndRetainsUsablePage()
        {
            using var directory = new TemporaryDirectory();
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider();
            var audio = new LearningPresentationTestEnvironment.ControlledAudioPlayer();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path, services =>
            {
                services.AddSingleton<ITtsProvider>(tts);
                services.AddSingleton<IAudioPlayer>(audio);
            });
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.Japanese, ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), Token);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                staged.Batch.BatchId, new[] { Assert.Single(staged.Batch.Items).ItemId }, ImportDecision.Learning), Token);
            await provider.GetRequiredService<LearningPlanService>().SaveAsync(new SaveLearningPlanRequestDto(
                LearningLanguage.Japanese, 20, 20, LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null,
                new[] { "Polite" }, new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), Token);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            CancellationToken speechToken = default;
            tts.BeforeReturnAsync = async token =>
            {
                speechToken = token;
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            };
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            var starting = nav.OpenLearningAsync(LearningLanguage.Japanese, timeout.Token);
            try
            {
                await entered.Task.WaitAsync(timeout.Token);
                var session = Assert.IsType<PracticeSessionViewModel>(nav.CurrentPage);
                var question = session.Question;
                nav.CompleteTransition();
                await nav.OpenSettingsAsync(Token);
                Assert.True(speechToken.IsCancellationRequested);
                await starting.WaitAsync(timeout.Token);
                Assert.Empty(audio.Played);
                nav.CompleteTransition();
                await nav.GoBackAsync(Token);
                nav.CompleteTransition();
                Assert.Same(session, nav.CurrentPage);
                Assert.Same(question, session.Question);
                tts.BeforeReturnAsync = null;
                await session.ReplayQuestionAsync(Token);
                Assert.Single(audio.Played);
            }
            finally
            {
                timeout.Cancel();
                await Record.ExceptionAsync(() => starting);
            }
        }

        private static async Task OpenPageAsync(NavigationService nav, string page, LearningLanguage language)
        {
            switch (page)
            {
                case "Lookup": await nav.OpenLexiconLookupAsync(language, Token); break;
                case "LearningPlan": await nav.OpenLearningPlanAsync(language, Token); break;
                case "ReviewPlan": await nav.OpenReviewPlanAsync(language, Token); break;
                case "ReviewLibrary": await nav.OpenReviewLibraryAsync(language, Token); break;
                case "TextImport": await nav.OpenTextImportAsync(language, cancellationToken: Token); break;
                case "Inbox": await nav.OpenImportInboxAsync(language, Token); break;
                case "WrongAnswers": await nav.OpenWrongAnswersAsync(language, Token); break;
                case "Statistics": await nav.OpenStatisticsAsync(language, Token); break;
                case "KanaPlan": await nav.OpenKanaAsync(language, Token); break;
                case "KanaSession":
                    await nav.OpenKanaAsync(language, Token);
                    nav.CompleteTransition();
                    await nav.OpenKanaSessionAsync(Token);
                    break;
                case "LearningSession": await nav.OpenLearningAsync(language, Token); break;
                case "ReviewSession": await nav.OpenReviewSessionAsync(language, Token); break;
                case "Mcp": await nav.OpenMcpSettingsAsync(Token); break;
                default: throw new ArgumentOutOfRangeException(nameof(page));
            }
        }

        [Fact]
        public async Task Inbox_SettingsNestedDecision_RefreshesOriginalPendingBatchesAndPreservesSelection()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var staging = provider.GetRequiredService<IImportStaging>();
            var first = await staging.StageAsync(new StageImportBatchRequestDto(LearningLanguage.Japanese,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), Token);
            var second = await staging.StageAsync(new StageImportBatchRequestDto(LearningLanguage.Japanese,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto("食前", "jmdict:2:1") }), Token);
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            await nav.OpenImportInboxAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            var original = Assert.IsType<ImportInboxListViewModel>(nav.CurrentPage);
            original.SelectedBatch = original.Batches.Single(batch => batch.BatchId == second.Batch.BatchId);
            original.IsAllSelected = true;
            await nav.OpenSettingsAsync(Token);
            nav.CompleteTransition();
            await nav.OpenImportInboxAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            var nested = Assert.IsType<ImportInboxListViewModel>(nav.CurrentPage);
            nested.SelectedBatch = nested.Batches.Single(batch => batch.BatchId == first.Batch.BatchId);
            nested.IsAllSelected = true;
            await nested.DecideAsync(ImportDecision.Learning, Token);
            Assert.Null(nested.ErrorMessage);
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.Same(original, nav.CurrentPage);
            Assert.Equal(second.Batch.BatchId, Assert.Single(original.Batches).BatchId);
            Assert.Equal(second.Batch.BatchId, original.SelectedBatch?.BatchId);
            Assert.True(Assert.Single(original.Items).IsSelected);
        }

        [Fact]
        public async Task LearningSession_SettingsAfterSubmissionCommitted_PreservesFeedbackAndCanContinue()
        {
            using var directory = new TemporaryDirectory();
            DelayedPracticeSubmissionWorkflow delayed = null!;
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path, services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                services.AddScoped<PracticeSessionViewModel>(p =>
                {
                    delayed = new DelayedPracticeSubmissionWorkflow(new LearningPracticeWorkflow(
                        p.GetRequiredService<ILearningSessionOperations>(), p.GetRequiredService<ILearningPlanReader>()));
                    return new PracticeSessionViewModel(delayed, p.GetRequiredService<TtsService>(), p.GetRequiredService<IAudioPlayer>());
                });
            });
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.Japanese, ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), Token);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                staged.Batch.BatchId, new[] { Assert.Single(staged.Batch.Items).ItemId }, ImportDecision.Learning), Token);
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            await nav.OpenLearningAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            var session = Assert.IsType<PracticeSessionViewModel>(nav.CurrentPage);
            session.AnswerText = "test";
            Assert.True(session.SubmitCommand.CanExecute(null));
            var submitting = session.SubmitAsync(Token);
            try
            {
                await delayed.Committed.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
                await nav.OpenSettingsAsync(Token);
                delayed.Release.TrySetResult();
                Assert.Null(await Record.ExceptionAsync(() => submitting));
                nav.CompleteTransition();
                await nav.GoBackAsync(Token);
                nav.CompleteTransition();
                Assert.Same(session, nav.CurrentPage);
                Assert.NotNull(session.Feedback);
                Assert.True(session.ContinueCommand.CanExecute(null));
                await session.ContinueAsync(Token);
                Assert.Null(session.ErrorMessage);
                Assert.Null(session.Feedback);
            }
            finally
            {
                delayed.Release.TrySetResult();
                await Record.ExceptionAsync(() => submitting);
            }
        }
    }
}
