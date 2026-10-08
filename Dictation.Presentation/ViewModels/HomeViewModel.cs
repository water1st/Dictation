using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 承载首页语言和主题选择，并通过首页导航能力打开页面。
    /// </summary>
    public sealed class HomeViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService settingsService;
        private readonly IHomeNavigation homeNavigation;
        private readonly ILearningProgressReader learningProgressReader;
        private readonly ILearningPlanReader learningPlanReader;
        private readonly ILearningSessionReader learningSessionReader;
        private readonly IReviewProgressReader reviewProgressReader;
        private readonly SemaphoreSlim persistenceGate = new(1, 1);
        private ThemeMode selectedThemeMode = ThemeMode.System;
        private LearningLanguage selectedLanguage = LearningLanguage.Japanese;
        private string learningProgressLabel = "学习进度暂不可用";
        private string reviewProgressLabel = "复习进度暂不可用";
        private string? reviewProgressErrorMessage;
        private long reviewRefreshVersion;
        private string? errorMessage;
        private bool isInitialized;

        /// <summary>
        /// 承载首页语言和主题选择，并通过首页导航能力打开页面。
        /// </summary>
        /// <param name="settingsService">读取和保存应用设置的业务服务。</param>
        /// <param name="homeNavigation">首页所需的业务页面导航能力。</param>
        /// <param name="learningProgressReader">读取当前语言每日学习进度的业务能力。</param>
        /// <param name="learningPlanReader">判断当前语言是否要求手动选词的计划读取能力。</param>
        /// <param name="learningSessionReader">判断当前语言是否有应优先恢复的活动会话。</param>
        /// <param name="reviewProgressReader">只读查询当前语言每日复习进度的能力。</param>
        public HomeViewModel(
            SettingsService settingsService,
            IHomeNavigation homeNavigation,
            ILearningProgressReader learningProgressReader,
            ILearningPlanReader learningPlanReader,
            ILearningSessionReader learningSessionReader,
            IReviewProgressReader reviewProgressReader)
        {
            this.settingsService = settingsService
                ?? throw new ArgumentNullException(nameof(settingsService));
            this.homeNavigation = homeNavigation
                ?? throw new ArgumentNullException(nameof(homeNavigation));
            this.learningProgressReader = learningProgressReader
                ?? throw new ArgumentNullException(nameof(learningProgressReader));
            this.learningPlanReader = learningPlanReader
                ?? throw new ArgumentNullException(nameof(learningPlanReader));
            this.learningSessionReader = learningSessionReader
                ?? throw new ArgumentNullException(nameof(learningSessionReader));
            this.reviewProgressReader = reviewProgressReader
                ?? throw new ArgumentNullException(nameof(reviewProgressReader));
            SelectJapaneseCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.Japanese));
            SelectEnglishCommand = new AsyncCommand(() => SelectLanguageAsync(LearningLanguage.English));
            SelectSystemThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.System));
            SelectLightThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Light));
            SelectDarkThemeCommand = new AsyncCommand(() => SelectThemeAsync(ThemeMode.Dark));
            OpenLexiconLookupCommand = new AsyncCommand(() => OpenLexiconLookupAsync());
            OpenTextImportCommand = new AsyncCommand(() => OpenTextImportAsync());
            OpenImportInboxCommand = new AsyncCommand(() => OpenImportInboxAsync());
            OpenLearningPlanCommand = new AsyncCommand(() => OpenLearningPlanAsync());
            OpenLearningCommand = new AsyncCommand(() => OpenLearningAsync());
            OpenReviewLibraryCommand = new AsyncCommand(() => OpenReviewLibraryAsync());
            OpenReviewPlanCommand = new AsyncCommand(() => OpenReviewPlanAsync());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LearningLanguage SelectedLanguage => selectedLanguage;

        public ThemeMode SelectedThemeMode => selectedThemeMode;

        public bool IsJapaneseSelected => selectedLanguage == LearningLanguage.Japanese;

        public bool IsEnglishSelected => selectedLanguage == LearningLanguage.English;

        public string CurrentLanguageLabel => selectedLanguage == LearningLanguage.Japanese
            ? "日语模式"
            : "英语模式";

        public string LearningProgressLabel
        {
            get => learningProgressLabel;
            private set
            {
                if (learningProgressLabel == value)
                {
                    return;
                }

                learningProgressLabel = value;
                OnPropertyChanged();
            }
        }

        public bool IsSystemThemeSelected => selectedThemeMode == ThemeMode.System;

        public string ReviewProgressLabel
        {
            get => reviewProgressLabel;
            private set
            {
                if (reviewProgressLabel == value) return;
                reviewProgressLabel = value;
                OnPropertyChanged();
            }
        }

        public string? ReviewProgressErrorMessage
        {
            get => reviewProgressErrorMessage;
            private set
            {
                if (reviewProgressErrorMessage == value) return;
                reviewProgressErrorMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>刷新当前语言复习进度，不发起答题或修改记忆。</summary>
        /// <param name="cancellationToken">取消当前查询。</param>
        public async Task RefreshReviewProgressAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var language = selectedLanguage;
            var version = Interlocked.Increment(ref reviewRefreshVersion);
            try
            {
                var progress = await reviewProgressReader.GetTodayAsync(language, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentReviewRefresh(language, version)) return;
                if (progress.Language != language)
                {
                    throw new InvalidDataException("每日复习进度的语言与首页不一致。");
                }
                ReviewProgressLabel = $"复习 {progress.CompletedGroupCount}/{progress.DailyTarget}";
                ReviewProgressErrorMessage = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!IsCurrentReviewRefresh(language, version)) return;
                ReviewProgressLabel = "复习进度暂不可用";
                ReviewProgressErrorMessage = $"读取复习进度失败：{exception.Message}";
            }
        }

        /// <summary>只允许当前语言的最新查询发布结果，包括同语言重复刷新。</summary>
        /// <param name="language">该查询开始时的语言。</param>
        /// <param name="version">该查询开始时的顺序标识。</param>
        private bool IsCurrentReviewRefresh(LearningLanguage language, long version) =>
            language == selectedLanguage && version == Volatile.Read(ref reviewRefreshVersion);

        /// <summary>刷新首页两个独立模式的每日进度。</summary>
        /// <param name="cancellationToken">取消首页读取。</param>
        public async Task RefreshDailyProgressAsync(CancellationToken cancellationToken = default)
        {
            await RefreshLearningProgressAsync(cancellationToken);
            await RefreshReviewProgressAsync(cancellationToken);
        }

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

        public ICommand OpenTextImportCommand { get; }

        public ICommand OpenImportInboxCommand { get; }

        public ICommand OpenLearningPlanCommand { get; }

        public ICommand OpenLearningCommand { get; }
        public ICommand OpenReviewLibraryCommand { get; }
        public ICommand OpenReviewPlanCommand { get; }

        /// <summary>按首页当前语言打开独立复习计划。</summary>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public Task OpenReviewPlanAsync(CancellationToken cancellationToken = default) => homeNavigation.OpenReviewPlanAsync(selectedLanguage, cancellationToken);

        /// <summary>按首页选定语言打开只读复习词库。</summary>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public Task OpenReviewLibraryAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenReviewLibraryAsync(selectedLanguage, cancellationToken);
        }

        /// <summary>
        /// 重新读取当前语言的每日学习进度。
        /// </summary>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        public async Task RefreshLearningProgressAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var language = selectedLanguage;
            try
            {
                var progress = await learningProgressReader.GetTodayAsync(language, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (selectedLanguage != language)
                {
                    return;
                }

                if (progress.Language != language)
                {
                    throw new InvalidDataException("每日学习进度的语言与首页不一致。");
                }

                LearningProgressLabel = $"学习 {progress.CompletedGroupCount}/{progress.DailyTarget}";
                if (ErrorMessage?.StartsWith("读取学习进度失败", StringComparison.Ordinal) == true)
                {
                    ErrorMessage = null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (selectedLanguage == language)
                {
                    LearningProgressLabel = "学习进度暂不可用";
                    ErrorMessage = $"读取学习进度失败：{exception.Message}";
                }
            }
        }

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
                    await RefreshDailyProgressAsync(cancellationToken);
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
        public async Task SelectLanguageAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            await SaveSettingsAsync(language, selectedThemeMode, cancellationToken);
            if (selectedLanguage == language)
            {
                await RefreshDailyProgressAsync(cancellationToken);
            }
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

        /// <summary>按首页当前语言打开文本词表导入页面。</summary>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public Task OpenTextImportAsync(CancellationToken cancellationToken = default)
        {
            return homeNavigation.OpenTextImportAsync(selectedLanguage, cancellationToken: cancellationToken);
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
        public async Task OpenLearningAsync(CancellationToken cancellationToken = default)
        {
            var language = selectedLanguage;
            var active = await learningSessionReader.GetActiveAsync(language, cancellationToken);
            if (active is null)
            {
                var plan = await learningPlanReader.LoadAsync(language, cancellationToken);
                if (plan.SelectionMode == LearningSelectionMode.Manual)
                {
                    await homeNavigation.OpenLearningPlanAsync(language, cancellationToken);
                    return;
                }
            }

            await homeNavigation.OpenLearningAsync(language, cancellationToken);
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
                // 切换后旧请求即使先于新读取结束，也不能把上一语言的计数写回首页。
                Interlocked.Increment(ref reviewRefreshVersion);
                ReviewProgressLabel = "正在读取复习进度";
                ReviewProgressErrorMessage = null;
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
