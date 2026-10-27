using Dictation.Core.Configuration;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>通过真实配置保存和页面状态验证语言入口的事务顺序。</summary>
    public sealed class LandingViewModelTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task SelectLanguage_SaveSucceeds_NavigatesWithSameLanguage(LearningLanguage language)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            var navigation = new WorkspaceNavigationProbe();
            var landing = new LandingViewModel(home, navigation);
            await landing.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
            Assert.Equal(language, home.SelectedLanguage);
            Assert.Equal(language, Assert.Single(navigation.Languages));
        }

        [Fact]
        public async Task SelectLanguage_SaveFailsThenRetry_ShowsErrorAndCanNavigate()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            var paths = provider.GetRequiredService<ApplicationPaths>();
            Directory.CreateDirectory(paths.SettingsFile);
            var navigation = new WorkspaceNavigationProbe();
            var landing = new LandingViewModel(home, navigation);
            await landing.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            Assert.Equal(LearningLanguage.Japanese, home.SelectedLanguage);
            Assert.NotNull(home.ErrorMessage);
            Assert.Empty(navigation.Languages);
            await landing.SelectLanguageAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Null(home.ErrorMessage);
            Assert.Equal(LearningLanguage.Japanese, Assert.Single(navigation.Languages));
        }

        [Fact]
        public async Task SelectLanguage_InvalidLanguageOrCancellation_DoesNotNavigate()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var navigation = new WorkspaceNavigationProbe();
            var landing = new LandingViewModel(provider.GetRequiredService<HomeViewModel>(), navigation);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => landing.SelectLanguageAsync((LearningLanguage)99, TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => landing.SelectLanguageAsync(LearningLanguage.Japanese, new CancellationToken(true)));
            Assert.Empty(navigation.Languages);
        }

        [Fact]
        public async Task SelectLanguage_ConcurrentCommands_OnlyFirstNavigatesAndTemporarilyDisablesCommands()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var navigation = new WorkspaceNavigationProbe { Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
            var landing = new LandingViewModel(provider.GetRequiredService<HomeViewModel>(), navigation);
            var first = landing.SelectLanguageAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            await navigation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.False(landing.SelectJapaneseCommand.CanExecute(null));
            Assert.False(landing.SelectEnglishCommand.CanExecute(null));
            await landing.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            navigation.Completion.SetResult();
            await first;
            Assert.Single(navigation.Languages);
            Assert.True(landing.SelectEnglishCommand.CanExecute(null));
        }
    }
}
