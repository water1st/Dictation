using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Core.Configuration;

namespace Dictation.Presentation.ViewModels
{
    public sealed class HomeViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService settingsService;
        private readonly NavigationService? navigationService;
        private readonly SemaphoreSlim persistenceGate = new(1, 1);
        private ThemeMode selectedThemeMode = ThemeMode.System;
        private LearningLanguage selectedLanguage = LearningLanguage.Japanese;
        private string? errorMessage;
        private bool isInitialized;

        public HomeViewModel(SettingsService settingsService)
            : this(settingsService, null)
        {
        }

        public HomeViewModel(
            SettingsService settingsService,
            NavigationService? navigationService)
        {
            this.settingsService = settingsService
                ?? throw new ArgumentNullException(nameof(settingsService));
            this.navigationService = navigationService;
            SelectJapaneseCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.Japanese));
            SelectEnglishCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.English));
            SelectSystemThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.System));
            SelectLightThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Light));
            SelectDarkThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Dark));
            OpenLexiconLookupCommand = new AsyncCommand(() => OpenLexiconLookupAsync());
            OpenImportInboxCommand = new AsyncCommand(() => OpenImportInboxAsync());
            OpenLearningPlanCommand = new AsyncCommand(() => OpenLearningPlanAsync());
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

        public Task SelectLanguageAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            return SaveSettingsAsync(language, selectedThemeMode, cancellationToken);
        }

        public Task SelectThemeAsync(
            ThemeMode themeMode,
            CancellationToken cancellationToken = default)
        {
            ValidateThemeMode(themeMode);
            return SaveSettingsAsync(selectedLanguage, themeMode, cancellationToken);
        }

        public Task OpenLexiconLookupAsync(CancellationToken cancellationToken = default)
        {
            return GetNavigationService().OpenLexiconLookupAsync(
                selectedLanguage,
                cancellationToken);
        }

        public Task OpenImportInboxAsync(CancellationToken cancellationToken = default)
        {
            return GetNavigationService().OpenImportInboxAsync(
                selectedLanguage,
                cancellationToken);
        }

        public Task OpenLearningPlanAsync(CancellationToken cancellationToken = default)
        {
            return GetNavigationService().OpenLearningPlanAsync(selectedLanguage, cancellationToken);
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

        private NavigationService GetNavigationService()
        {
            return navigationService
                ?? throw new InvalidOperationException("首页导航服务尚未配置。");
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
