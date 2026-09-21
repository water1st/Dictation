using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Core.Configuration;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 承载首页语言和主题选择，并通过首页导航能力打开页面。
    /// </summary>
    public sealed class HomeViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService settingsService;
        private readonly IHomeNavigation homeNavigation;
        private readonly SemaphoreSlim persistenceGate = new(1, 1);
        private ThemeMode selectedThemeMode = ThemeMode.System;
        private LearningLanguage selectedLanguage = LearningLanguage.Japanese;
        private string? errorMessage;
        private bool isInitialized;

        /// <summary>
        /// 承载首页语言和主题选择，并通过首页导航能力打开页面。
        /// </summary>
        /// <param name="settingsService">读取和保存应用设置的业务服务。</param>
        /// <param name="homeNavigation">首页所需的业务页面导航能力。</param>
        public HomeViewModel(
            SettingsService settingsService,
            IHomeNavigation homeNavigation)
        {
            this.settingsService = settingsService
                ?? throw new ArgumentNullException(nameof(settingsService));
            this.homeNavigation = homeNavigation
                ?? throw new ArgumentNullException(nameof(homeNavigation));
            SelectJapaneseCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.Japanese));
            SelectEnglishCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.English));
            SelectSystemThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.System));
            SelectLightThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Light));
            SelectDarkThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Dark));
            OpenLexiconLookupCommand = new AsyncCommand(() => OpenLexiconLookupAsync());
            OpenImportInboxCommand = new AsyncCommand(() => OpenImportInboxAsync());
            OpenLearningPlanCommand = new AsyncCommand(() => OpenLearningPlanAsync());
            OpenLearningCommand = new AsyncCommand(() => OpenLearningAsync());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LearningLanguage SelectedLanguage => selectedLanguage;

        public ThemeMode SelectedThemeMode => selectedThemeMode;

        public bool IsJapaneseSelected => selectedLanguage == LearningLanguage.Japanese;

        public bool IsEnglishSelected => selectedLanguage == LearningLanguage.English;

        public string CurrentLanguageLabel => selectedLanguage == LearningLanguage.Japanese
            ? "日语模式"
            : "英语模式";

        public bool IsSystemThemeSelected => selectedThemeMode == ThemeMode.System;

        public bool IsLightThemeSelected => selectedThemeMode == ThemeMode.Light;

        public bool IsDarkThemeSelected => selectedThemeMode == ThemeMode.Dark;

        public string? ErrorMessage
        {
            get => errorMessage;
            private set
            {
                if (errorMessage == value)
                {
                    return;
                }

                errorMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand SelectJapaneseCommand { get; }

        public ICommand SelectEnglishCommand { get; }

        public ICommand SelectSystemThemeCommand { get; }

        public ICommand SelectLightThemeCommand { get; }

        public ICommand SelectDarkThemeCommand { get; }

        public ICommand OpenLexiconLookupCommand { get; }

        public ICommand OpenImportInboxCommand { get; }

        public ICommand OpenLearningPlanCommand { get; }

        public ICommand OpenLearningCommand { get; }

        /// <summary>
        /// 读取保存的语言与主题选择，并更新首页显示状态。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (isInitialized)
            {
                return;
            }

            await persistenceGate.WaitAsync(cancellationToken);
            try
            {
                if (isInitialized)
                {
                    return;
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var settings = settingsService.Load();
                    ApplySettings(settings);
                    ErrorMessage = null;
                    isInitialized = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    ErrorMessage = $"加载设置失败：{exception.Message}";
                }
            }
            finally
            {
                persistenceGate.Release();
            }
        }

        /// <summary>
        /// 保存语言选择并更新首页显示；失败时保留错误信息。
        /// </summary>
        /// <param name="language">当前学习语言。</param>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task SelectLanguageAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            return SaveSettingsAsync(language, selectedThemeMode, cancellationToken);
        }

        /// <summary>
        /// 保存主题选择并更新绑定状态；失败时保留错误信息。
        /// </summary>
        /// <param name="themeMode">待保存的主题选择。</param>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task SelectThemeAsync(
            ThemeMode themeMode,
            CancellationToken cancellationToken = default)
        {
            ValidateThemeMode(themeMode);
            return SaveSettingsAsync(selectedLanguage, themeMode, cancellationToken);
        }

        /// <summary>
        /// 按当前语言请求打开词库查询页面。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task OpenLexiconLookupAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenLexiconLookupAsync(
                selectedLanguage,
                cancellationToken);
        }

        /// <summary>
        /// 按当前语言请求打开导入收件箱。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task OpenImportInboxAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenImportInboxAsync(
                selectedLanguage,
                cancellationToken);
        }

        /// <summary>
        /// 按当前语言请求打开学习计划页面。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task OpenLearningPlanAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenLearningPlanAsync(selectedLanguage, cancellationToken);
        }

        /// <summary>
        /// 按当前语言请求打开或恢复学习会话。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task OpenLearningAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenLearningAsync(selectedLanguage, cancellationToken);
        }

        private async Task SaveSettingsAsync(
            LearningLanguage language,
            ThemeMode themeMode,
            CancellationToken cancellationToken)
        {
            await persistenceGate.WaitAsync(cancellationToken);
            try
            {
                if (language == selectedLanguage && themeMode == selectedThemeMode)
                {
                    return;
                }

                var settings = new ApplicationSettingsDto(themeMode, language);
                try
                {
                    await settingsService.SaveAsync(settings, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    ErrorMessage = $"保存设置失败：{exception.Message}";
                    return;
                }

                ApplySettings(settings);
                ErrorMessage = null;
            }
            finally
            {
                persistenceGate.Release();
            }
        }

        private void ApplySettings(ApplicationSettingsDto settings)
        {
            ValidateLanguage(settings.LastLanguage);
            ValidateThemeMode(settings.ThemeMode);

            var languageChanged = selectedLanguage != settings.LastLanguage;
            var themeChanged = selectedThemeMode != settings.ThemeMode;
            selectedLanguage = settings.LastLanguage;
            selectedThemeMode = settings.ThemeMode;

            if (languageChanged)
            {
                OnPropertyChanged(nameof(SelectedLanguage));
                OnPropertyChanged(nameof(IsJapaneseSelected));
                OnPropertyChanged(nameof(IsEnglishSelected));
                OnPropertyChanged(nameof(CurrentLanguageLabel));
            }

            if (themeChanged)
            {
                OnPropertyChanged(nameof(SelectedThemeMode));
                OnPropertyChanged(nameof(IsSystemThemeSelected));
                OnPropertyChanged(nameof(IsLightThemeSelected));
                OnPropertyChanged(nameof(IsDarkThemeSelected));
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "语言值无效。");
            }
        }

        private static void ValidateThemeMode(ThemeMode themeMode)
        {
            if (themeMode is not (ThemeMode.System or ThemeMode.Light or ThemeMode.Dark))
            {
                throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "主题值无效。");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
