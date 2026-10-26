using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Presentation.Navigation;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>保存语言选择后进入相应工作页，复用应用级业务状态。</summary>
    public sealed class LandingViewModel
    {
        private readonly HomeViewModel home;
        private readonly IWorkspaceNavigation navigation;
        private bool isSelecting;

        public LandingViewModel(HomeViewModel home, IWorkspaceNavigation navigation)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            SelectJapaneseCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.Japanese), () => !isSelecting);
            SelectEnglishCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.English), () => !isSelecting);
        }

        public HomeViewModel Home => home;
        public ICommand SelectJapaneseCommand { get; }
        public ICommand SelectEnglishCommand { get; }

        /// <summary>在语言保存成功后导航；取消由调用方处理。</summary>
        public async Task SelectLanguageAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            if (isSelecting) return;
            isSelecting = true;
            NotifyCommands();
            try
            {
                await home.SelectLanguageAsync(language, cancellationToken);
                if (home.SelectedLanguage == language && home.ErrorMessage is null)
                    await navigation.OpenLanguageWorkspaceAsync(language, cancellationToken);
            }
            finally
            {
                isSelecting = false;
                NotifyCommands();
            }
        }

        private void NotifyCommands()
        {
            ((AsyncCommand)SelectJapaneseCommand).NotifyCanExecuteChanged();
            ((AsyncCommand)SelectEnglishCommand).NotifyCanExecuteChanged();
        }
    }
}
