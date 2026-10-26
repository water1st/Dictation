using System.ComponentModel;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Presentation.Navigation;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>持有设置分类和临时目标语言，复用现有主题及 MCP 能力。</summary>
    public sealed class SettingsViewModel : INotifyPropertyChanged
    {
        public SettingsViewModel(HomeViewModel home, IHomeNavigation navigation, McpSettingsViewModel mcp)
        {
            ArgumentNullException.ThrowIfNull(home);
            ArgumentNullException.ThrowIfNull(navigation);
            ArgumentNullException.ThrowIfNull(mcp);
            Home = home;
            Mcp = mcp;
            SettingsTargetLanguage = home.SelectedLanguage;
            SelectAppearanceCommand = new AsyncCommand(() => SelectSectionAsync(SettingsSection.Appearance));
            SelectImportCommand = new AsyncCommand(() => SelectSectionAsync(SettingsSection.Import));
            SelectLearningReviewCommand = new AsyncCommand(() => SelectSectionAsync(SettingsSection.LearningReview));
            SelectMcpCommand = new AsyncCommand(() => SelectSectionAsync(SettingsSection.Mcp));
            SelectJapaneseTargetCommand = new AsyncCommand(() => SelectTargetLanguageAsync(LearningLanguage.Japanese));
            SelectEnglishTargetCommand = new AsyncCommand(() => SelectTargetLanguageAsync(LearningLanguage.English));
            OpenTextImportCommand = new AsyncCommand(() => navigation.OpenTextImportAsync(SettingsTargetLanguage));
            OpenImportInboxCommand = new AsyncCommand(() => navigation.OpenImportInboxAsync(SettingsTargetLanguage));
            OpenLearningPlanCommand = new AsyncCommand(() => navigation.OpenLearningPlanAsync(SettingsTargetLanguage));
            OpenReviewPlanCommand = new AsyncCommand(() => navigation.OpenReviewPlanAsync(SettingsTargetLanguage));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public HomeViewModel Home { get; }
        public McpSettingsViewModel Mcp { get; }
        public SettingsSection SelectedSection { get; private set; }
        public LearningLanguage SettingsTargetLanguage { get; private set; }
        public bool IsJapaneseTarget => SettingsTargetLanguage == LearningLanguage.Japanese;
        public bool IsEnglishTarget => SettingsTargetLanguage == LearningLanguage.English;
        public ICommand SelectAppearanceCommand { get; }
        public ICommand SelectImportCommand { get; }
        public ICommand SelectLearningReviewCommand { get; }
        public ICommand SelectMcpCommand { get; }
        public ICommand SelectJapaneseTargetCommand { get; }
        public ICommand SelectEnglishTargetCommand { get; }
        public ICommand OpenTextImportCommand { get; }
        public ICommand OpenImportInboxCommand { get; }
        public ICommand OpenLearningPlanCommand { get; }
        public ICommand OpenReviewPlanCommand { get; }

        /// <summary>切换分类，进入 MCP 时协调现有刷新，离开时取消未完成操作。</summary>
        public async Task SelectSectionAsync(SettingsSection section, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(section)) throw new ArgumentOutOfRangeException(nameof(section));
            if (SelectedSection != section)
            {
                if (SelectedSection == SettingsSection.Mcp) Mcp.CancelOperation();
                SelectedSection = section;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSection)));
            }
            if (section == SettingsSection.Mcp && !Mcp.IsBusy) await Mcp.RefreshAsync(cancellationToken);
        }

        /// <summary>只改变本设置实例的操作语言，不写入应用语言选择。</summary>
        public Task SelectTargetLanguageAsync(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
                throw new ArgumentOutOfRangeException(nameof(language));
            SettingsTargetLanguage = language;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SettingsTargetLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsJapaneseTarget)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglishTarget)));
            return Task.CompletedTask;
        }

        /// <summary>离开设置时立即取消内部异步操作，作用域由导航转场释放。</summary>
        public void CancelOperation() => Mcp.CancelOperation();
    }
}
