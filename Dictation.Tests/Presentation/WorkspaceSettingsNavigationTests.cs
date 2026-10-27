using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Presentation.Mcp;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证新页面层次、设置上下文和原业务页之间的真实导航。</summary>
    public sealed class WorkspaceSettingsNavigationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task Workspace_RoundTripAfterLanguageSelection_ReusesSingletonAndRootHasNoBack(LearningLanguage language)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            Assert.False(nav.CanGoBack);
            await root.SelectLanguageAsync(language, Token);
            Assert.Same(home, nav.CurrentPage);
            Assert.Equal(language == LearningLanguage.Japanese ? "日语学习" : "英语学习", nav.PageTitle);
            nav.CompleteTransition();
            await home.OpenLexiconLookupAsync(Token);
            Assert.Equal(language, Assert.IsType<LexiconLookupViewModel>(nav.CurrentPage).SelectedLanguage);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            Assert.Same(home, nav.CurrentPage);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            Assert.Same(root, nav.CurrentPage);
            nav.CompleteTransition();
            Assert.False(nav.CanGoBack);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Settings_OpenImportAndPlansAcrossLanguages_ReturnsToSameSectionAndPreservesWorkspaceLanguage(bool fromWorkspace)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            if (fromWorkspace) { await root.SelectLanguageAsync(LearningLanguage.Japanese, Token); nav.CompleteTransition(); }
            var origin = nav.CurrentPage;
            await nav.OpenSettingsAsync(Token);
            var settings = Assert.IsType<SettingsViewModel>(nav.CurrentPage);
            nav.CompleteTransition();
            await settings.SelectSectionAsync(SettingsSection.Import, Token);
            await settings.SelectTargetLanguageAsync(LearningLanguage.English);
            Assert.True(settings.IsEnglishTarget);
            Assert.Equal(LearningLanguage.Japanese, home.SelectedLanguage);
            foreach (var open in new Func<Task>[] {
                () => nav.OpenTextImportAsync(settings.SettingsTargetLanguage, cancellationToken: Token),
                () => nav.OpenImportInboxAsync(settings.SettingsTargetLanguage, Token),
                () => nav.OpenLearningPlanAsync(settings.SettingsTargetLanguage, Token),
                () => nav.OpenReviewPlanAsync(settings.SettingsTargetLanguage, Token) })
            {
                await open();
                Assert.StartsWith("英语", nav.PageTitle);
                nav.CompleteTransition();
                await nav.GoBackAsync(Token);
                Assert.Same(settings, nav.CurrentPage);
                Assert.Equal(SettingsSection.Import, settings.SelectedSection);
                Assert.True(settings.IsEnglishTarget);
                nav.CompleteTransition();
            }
            await settings.SelectSectionAsync(SettingsSection.Mcp, Token);
            Assert.Equal("47821", settings.Mcp.PortText);
            await nav.GoBackAsync(Token);
            Assert.Same(origin, nav.CurrentPage);
            await settings.Mcp.RefreshAsync(Token);
            nav.CompleteTransition();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => settings.Mcp.RefreshAsync(Token));
        }

        [Fact]
        public async Task Navigation_InvalidSourceLanguageOrCancellation_RejectsWithoutPageChange()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            await Assert.ThrowsAsync<InvalidOperationException>(() => nav.OpenLanguageWorkspaceAsync(LearningLanguage.English, Token));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => nav.OpenLanguageWorkspaceAsync((LearningLanguage)99, Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => nav.OpenSettingsAsync(new CancellationToken(true)));
            await root.SelectLanguageAsync(LearningLanguage.Japanese, Token);
            nav.CompleteTransition();
            await Assert.ThrowsAsync<InvalidOperationException>(() => nav.OpenLanguageWorkspaceAsync(LearningLanguage.Japanese, Token));
            await nav.OpenSettingsAsync(Token);
            nav.CompleteTransition();
            await Assert.ThrowsAsync<InvalidOperationException>(() => nav.OpenSettingsAsync(Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => nav.OpenStatisticsAsync(LearningLanguage.Japanese, Token));
        }

        internal static Task<ServiceProvider> CreateProviderAsync(string root, Action<IServiceCollection>? configure = null) => ReviewPlanPresentationTestHost.CreateAsync(root,
            configure: services =>
            {
                services.AddLogging();
                services.AddDictationMcp();
                services.AddScoped<McpSettingsViewModel>(p => new McpSettingsViewModel(p.GetRequiredService<McpSettingsService>(),
                    p.GetRequiredService<IMcpHostControl>(), p.GetRequiredService<IMcpStatusReader>(),
                    p.GetRequiredService<CodexMcpRegistrationService>(), Path.Combine(root, "client", "config.toml")));
                configure?.Invoke(services);
            });

        [Fact]
        public async Task LanguageSelection_OpenSettingsWhileProgressLoads_KeepsRootUntilWorkspaceReady()
        {
            using var directory = new TemporaryDirectory();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var provider = await CreateProviderAsync(directory.Path, services =>
                services.AddSingleton<ILearningProgressReader>(new StubLearningProgressReader
                {
                    ReadAsync = async (language, token) =>
                    {
                        entered.TrySetResult();
                        await completion.Task.WaitAsync(token);
                        return new LearningDailyProgressDto(language, 0, 0, 20);
                    }
                }));
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            var selecting = root.SelectLanguageAsync(LearningLanguage.Japanese, Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await nav.OpenSettingsAsync(Token);
            var duringSelection = nav.CurrentPage;
            completion.TrySetResult();
            var exception = await Record.ExceptionAsync(() => selecting);
            Assert.Null(exception);
            Assert.Same(root, duringSelection);
            Assert.Same(root.Home, nav.CurrentPage);
        }

        [Fact]
        public async Task SettingsEntry_RapidRepeatedClicks_PreservesPageDuringTransition()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            await nav.OpenSettingsAsync(Token);
            var settings = nav.CurrentPage;
            await nav.OpenSettingsAsync(Token);
            Assert.Same(settings, nav.CurrentPage);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            Assert.IsType<LandingViewModel>(nav.CurrentPage);
            Assert.False(nav.CanGoBack);
        }
    }
}
