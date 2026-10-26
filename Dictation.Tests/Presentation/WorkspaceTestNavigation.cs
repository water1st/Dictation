using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;

namespace Dictation.Tests.Presentation
{
    /// <summary>让原有业务页测试从固定语言工作页开始。</summary>
    internal static class WorkspaceTestNavigation
    {
        internal static void InitializeWorkspace(this NavigationService navigation, HomeViewModel home)
        {
            navigation.InitializeHome(new LandingViewModel(home, navigation));
            navigation.OpenLanguageWorkspaceAsync(home.SelectedLanguage, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            navigation.CompleteTransition();
        }
    }
}
