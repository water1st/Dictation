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
        public async Task 选择语言_保存成功_携带相同语言导航(LearningLanguage language)
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
        public async Task 选择语言_保存失败再选择原语言_显示错误后可恢复导航()
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
        public async Task 选择语言_非法语言或取消_拒绝且不导航()
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
        public async Task 选择语言_两个命令同时触发_仅首次进入且全部命令暂时禁用()
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
