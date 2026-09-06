using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;

namespace Dictation.Presentation.ViewModels
{
    public sealed class LearningPlanViewModel : INotifyPropertyChanged
    {
        private readonly LearningPlanService learningPlanService;
        private readonly LexiconLookupService lexiconLookupService;
        private readonly NavigationService navigationService;
        private readonly OverlayService overlayService;
        private readonly SemaphoreSlim operationGate = new(1, 1);
        private LearningLanguage selectedLanguage;
        private LearningSelectionMode selectionMode;
        private EnglishFormLabelMode englishFormLabelMode;
        private LexiconLookupResultDto? selectedExample;
        private string? selectedExampleSourceSenseId;
        private string exampleQuery = string.Empty;
        private bool? isAllSelected;
        private int dailyNewTarget;
        private int sessionWordLimit;
        private int pageNumber = 1;
        private int totalCount;
        private string? errorMessage;
        private string? statusMessage;
        private bool isInitialized;

        public LearningPlanViewModel(
            LearningPlanService learningPlanService,
            LexiconLookupService lexiconLookupService,
            NavigationService navigationService,
            OverlayService overlayService)
        {
            this.learningPlanService = learningPlanService ?? throw new ArgumentNullException(nameof(learningPlanService));
            this.lexiconLookupService = lexiconLookupService ?? throw new ArgumentNullException(nameof(lexiconLookupService));
            this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            this.overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            FormOptions = new ObservableCollection<LearningPlanOptionViewModel>();
            ChannelOptions = new ObservableCollection<LearningPlanOptionViewModel>();
            OutputOptions = new ObservableCollection<LearningPlanOptionViewModel>();
            LibraryItems = new ObservableCollection<LearningLibraryItemViewModel>();
            ExampleCandidates = new ObservableCollection<LexiconLookupResultDto>();
            BackCommand = new AsyncCommand(() => navigationService.GoBackAsync());
            RefreshCommand = new AsyncCommand(() => RefreshAsync());
            SaveCommand = new AsyncCommand(() => SaveAsync());
            SearchExamplesCommand = new AsyncCommand(() => SearchExamplesAsync());
            DeleteSelectedCommand = new AsyncCommand(() => DeleteSelectedAsync());
            StartCommand = new AsyncCommand(() => StartAsync());
            PreviousPageCommand = new AsyncCommand(() => PreviousPageAsync());
            NextPageCommand = new AsyncCommand(() => NextPageAsync());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LearningLanguage SelectedLanguage
        {
            get
            {
                EnsureInitialized();
                return selectedLanguage;
            }
        }

        public string WindowTitle => !isInitialized
            ? "学习计划"
            : selectedLanguage == LearningLanguage.Japanese ? "日语学习计划" : "英语学习计划";

        public string LanguageLabel => selectedLanguage == LearningLanguage.Japanese ? "日语" : "英语";

        public bool IsEnglish => isInitialized && selectedLanguage == LearningLanguage.English;

        public bool IsRandomSelection
        {
            get => SelectionMode == LearningSelectionMode.Random;
            set { if (value) SelectionMode = LearningSelectionMode.Random; }
        }

        public bool IsSequentialSelection
        {
            get => SelectionMode == LearningSelectionMode.Sequential;
            set { if (value) SelectionMode = LearningSelectionMode.Sequential; }
        }

        public bool IsManualSelection
        {
            get => SelectionMode == LearningSelectionMode.Manual;
            set { if (value) SelectionMode = LearningSelectionMode.Manual; }
        }

        public bool IsGrammarName
        {
            get => EnglishFormLabelMode == EnglishFormLabelMode.GrammarName;
            set { if (value) EnglishFormLabelMode = EnglishFormLabelMode.GrammarName; }
        }

        public bool IsExampleParadigm
        {
            get => EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm;
            set { if (value) EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm; }
        }

        public int DailyNewTarget
        {
            get => dailyNewTarget;
            set => SetField(ref dailyNewTarget, value);
        }

        public int SessionWordLimit
        {
            get => sessionWordLimit;
            set => SetField(ref sessionWordLimit, value);
        }

        public LearningSelectionMode SelectionMode
        {
            get => selectionMode;
            set
            {
                if (SetField(ref selectionMode, value))
                {
                    OnPropertyChanged(nameof(IsRandomSelection));
                    OnPropertyChanged(nameof(IsSequentialSelection));
                    OnPropertyChanged(nameof(IsManualSelection));
                }
            }
        }

        public EnglishFormLabelMode EnglishFormLabelMode
        {
            get => englishFormLabelMode;
            set
            {
                if (SetField(ref englishFormLabelMode, value))
                {
                    OnPropertyChanged(nameof(IsGrammarName));
                    OnPropertyChanged(nameof(IsExampleParadigm));
                }
            }
        }

        public string ExampleQuery
        {
            get => exampleQuery;
            set => SetField(ref exampleQuery, value ?? string.Empty);
        }

        public LexiconLookupResultDto? SelectedExample
        {
            get => selectedExample;
            set
            {
                if (SetField(ref selectedExample, value))
                {
                    selectedExampleSourceSenseId = value?.SourceSenseId;
                }
            }
        }

        public ObservableCollection<LearningPlanOptionViewModel> FormOptions { get; }

        public ObservableCollection<LearningPlanOptionViewModel> ChannelOptions { get; }

        public ObservableCollection<LearningPlanOptionViewModel> OutputOptions { get; }

        public ObservableCollection<LearningLibraryItemViewModel> LibraryItems { get; }

        public ObservableCollection<LexiconLookupResultDto> ExampleCandidates { get; }

        public bool? IsAllSelected
        {
            get => isAllSelected;
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                foreach (var item in LibraryItems)
                {
                    item.IsSelected = value.Value;
                }

                RecalculateAllSelected();
            }
        }

        public int PageNumber
        {
            get => pageNumber;
            private set
            {
                if (SetField(ref pageNumber, value))
                {
                    OnPropertyChanged(nameof(CanPreviousPage));
                    OnPropertyChanged(nameof(CanNextPage));
                }
            }
        }

        public int PageSize => 20;

        public int TotalCount
        {
            get => totalCount;
            private set
            {
                if (SetField(ref totalCount, value))
                {
                    OnPropertyChanged(nameof(CanNextPage));
                }
            }
        }

        public bool CanPreviousPage => PageNumber > 1;

        public bool CanNextPage => PageNumber * PageSize < TotalCount;

        public string? ErrorMessage
        {
            get => errorMessage;
            private set => SetField(ref errorMessage, value);
        }

        public string? StatusMessage
        {
            get => statusMessage;
            private set => SetField(ref statusMessage, value);
        }

        public LearningStartRequestDto? LastStartRequest { get; private set; }

        public ICommand BackCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SearchExamplesCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

        public void Initialize(LearningLanguage language)
        {
            ValidateLanguage(language);
            if (isInitialized)
            {
                throw new InvalidOperationException("学习计划已经初始化，不能切换语言。");
            }

            selectedLanguage = language;
            isInitialized = true;
            BuildOptions(language);
            ExampleQuery = language == LearningLanguage.English ? "do" : string.Empty;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(IsEnglish));
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(token => RefreshCoreAsync(resetPage: false, token), cancellationToken, "加载学习计划失败");
        }

        public Task RefreshPreservingStateAsync(CancellationToken cancellationToken = default)
        {
            return RefreshAsync(cancellationToken);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(SaveCoreAsync, cancellationToken, "保存学习计划失败");
        }

        public Task SearchExamplesAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(SearchExamplesCoreAsync, cancellationToken, "检索英语示例词失败");
        }

        public async Task DeleteSelectedAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            await operationGate.WaitAsync(cancellationToken);
            try
            {
                ErrorMessage = null;
                StatusMessage = null;
                var selected = LibraryItems.Where(item => item.IsSelected).ToArray();
                if (selected.Length == 0)
                {
                    ErrorMessage = "请至少选择一个要移除的词组。";
                    return;
                }

                var confirmed = await overlayService.ConfirmAsync(
                    "移除学习词组",
                    $"确定移除已选的 {selected.Length} 个整组词条及其学习进度吗？",
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!confirmed)
                {
                    return;
                }

                foreach (var item in selected)
                {
                    await learningPlanService.DeleteWordGroupAsync(selectedLanguage, item.WordGroupId, cancellationToken);
                }

                if (selected.Length == LibraryItems.Count && PageNumber > 1)
                {
                    PageNumber--;
                }

                await RefreshLibraryAsync(cancellationToken);
                StatusMessage = $"已移除 {selected.Length} 个整组词条。";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"移除学习词组失败：{exception.Message}";
            }
            finally
            {
                operationGate.Release();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(StartCoreAsync, cancellationToken, "准备本轮学习失败");
        }

        public Task PreviousPageAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(async token =>
            {
                if (!CanPreviousPage)
                {
                    return;
                }

                PageNumber--;
                await RefreshLibraryAsync(token);
            }, cancellationToken, "读取上一页失败");
        }

        public Task NextPageAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RunExclusiveAsync(async token =>
            {
                if (!CanNextPage)
                {
                    return;
                }

                PageNumber++;
                await RefreshLibraryAsync(token);
            }, cancellationToken, "读取下一页失败");
        }

        private async Task RefreshCoreAsync(bool resetPage, CancellationToken cancellationToken)
        {
            if (resetPage)
            {
                PageNumber = 1;
            }

            var plan = await learningPlanService.LoadAsync(selectedLanguage, cancellationToken);
            ApplyPlan(plan);
            await RefreshLibraryAsync(cancellationToken);
            StatusMessage = null;
        }

        private async Task RefreshLibraryAsync(CancellationToken cancellationToken)
        {
            var page = await learningPlanService.GetLibraryPageAsync(
                selectedLanguage,
                PageNumber,
                PageSize,
                cancellationToken);
            foreach (var item in LibraryItems)
            {
                item.PropertyChanged -= OnLibraryItemPropertyChanged;
            }

            LibraryItems.Clear();
            foreach (var group in page.Groups)
            {
                var item = new LearningLibraryItemViewModel(group);
                item.PropertyChanged += OnLibraryItemPropertyChanged;
                LibraryItems.Add(item);
            }

            TotalCount = page.TotalCount;
            RecalculateAllSelected();
        }

        private async Task SaveCoreAsync(CancellationToken cancellationToken)
        {
            var request = CreateSaveRequest();
            var saved = await learningPlanService.SaveAsync(request, cancellationToken);
            ApplyPlan(saved);
            StatusMessage = "学习计划已保存。";
        }

        private async Task SearchExamplesCoreAsync(CancellationToken cancellationToken)
        {
            if (!IsEnglish)
            {
                throw new InvalidOperationException("只有英语计划可以选择示例范式。");
            }

            var selectedForms = FormOptions.Where(option => option.IsSelected).Select(option => option.Code).ToArray();
            var results = await lexiconLookupService.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, ExampleQuery, 20),
                cancellationToken);
            SelectedExample = null;
            ExampleCandidates.Clear();
            foreach (var result in results.Where(result =>
                result.PartOfSpeechLabel == "动词"
                && !result.IsPhrase
                && selectedForms.All(code => result.Forms.Any(form => form.FormCode == code))))
            {
                ExampleCandidates.Add(result);
            }

            StatusMessage = ExampleCandidates.Count == 0 ? "没有找到包含全部已选词形的动词。" : null;
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            await SaveCoreAsync(cancellationToken);
            var selectedIds = SelectionMode == LearningSelectionMode.Manual
                ? LibraryItems.Where(item => item.IsSelected).Select(item => item.WordGroupId).ToArray()
                : null;
            LastStartRequest = await learningPlanService.CreateStartRequestAsync(
                selectedLanguage,
                selectedIds,
                cancellationToken);
            OnPropertyChanged(nameof(LastStartRequest));
            StatusMessage = "本轮学习范围已准备完成。";
        }

        private SaveLearningPlanRequestDto CreateSaveRequest()
        {
            return new SaveLearningPlanRequestDto(
                selectedLanguage,
                DailyNewTarget,
                SessionWordLimit,
                SelectionMode,
                EnglishFormLabelMode,
                IsEnglish && EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                    ? selectedExample?.SourceSenseId ?? selectedExampleSourceSenseId
                    : null,
                FormOptions.Where(option => option.IsSelected).Select(option => option.Code).ToArray(),
                ChannelOptions.Where(option => option.IsSelected)
                    .Select(option => Enum.Parse<LearningChannel>(option.Code)).ToArray(),
                OutputOptions.Where(option => option.IsSelected)
                    .Select(option => Enum.Parse<LearningOutputMode>(option.Code)).ToArray());
        }

        private void ApplyPlan(LearningPlanDto plan)
        {
            if (plan.Language != selectedLanguage)
            {
                throw new InvalidOperationException("服务返回了其他语言的学习计划。");
            }

            DailyNewTarget = plan.DailyNewTarget;
            SessionWordLimit = plan.SessionWordLimit;
            SelectionMode = plan.SelectionMode;
            EnglishFormLabelMode = plan.EnglishFormLabelMode;
            selectedExampleSourceSenseId = plan.EnglishExampleSourceSenseId;
            selectedExample = ExampleCandidates.FirstOrDefault(item => item.SourceSenseId == selectedExampleSourceSenseId);
            OnPropertyChanged(nameof(SelectedExample));
            ApplySelected(FormOptions, plan.FormCodes);
            ApplySelected(ChannelOptions, plan.Channels.Select(channel => channel.ToString()));
            ApplySelected(OutputOptions, plan.OutputModes.Select(output => output.ToString()));
        }

        private void BuildOptions(LearningLanguage language)
        {
            FormOptions.Clear();
            var forms = language == LearningLanguage.Japanese
                ? new[]
                {
                    ("Dictionary", "普通形"), ("Polite", "礼貌形"), ("Te", "て形"), ("Ta", "た形")
                }
                : new[]
                {
                    ("Base", "原形"), ("PresentFirstPersonSingular", "第一人称单数"),
                    ("PresentNonThirdPerson", "非第三人称现在式"), ("ThirdPersonSingular", "第三人称单数"),
                    ("Past", "过去式"), ("PastParticiple", "过去分词"), ("PresentParticiple", "现在分词"),
                    ("Plural", "复数"), ("Comparative", "比较级"), ("Superlative", "最高级")
                };
            foreach (var (code, label) in forms)
            {
                FormOptions.Add(new LearningPlanOptionViewModel(code, label));
            }

            ChannelOptions.Clear();
            ChannelOptions.Add(new LearningPlanOptionViewModel("MeaningWriting", "看义默写"));
            ChannelOptions.Add(new LearningPlanOptionViewModel("Dictation", "听写"));
            OutputOptions.Clear();
            if (language == LearningLanguage.Japanese)
            {
                OutputOptions.Add(new LearningPlanOptionViewModel("Kana", "假名"));
                OutputOptions.Add(new LearningPlanOptionViewModel("Kanji", "汉字"));
            }
            else
            {
                OutputOptions.Add(new LearningPlanOptionViewModel("Latin", "英文字母"));
            }
        }

        private static void ApplySelected(
            IEnumerable<LearningPlanOptionViewModel> options,
            IEnumerable<string> selectedCodes)
        {
            var selected = selectedCodes.ToHashSet(StringComparer.Ordinal);
            foreach (var option in options)
            {
                option.IsSelected = selected.Contains(option.Code);
            }
        }

        private async Task RunExclusiveAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken,
            string errorPrefix)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await operationGate.WaitAsync(cancellationToken);
            try
            {
                ErrorMessage = null;
                StatusMessage = null;
                await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"{errorPrefix}：{exception.Message}";
            }
            finally
            {
                operationGate.Release();
            }
        }

        private void OnLibraryItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LearningLibraryItemViewModel.IsSelected))
            {
                RecalculateAllSelected();
            }
        }

        private void RecalculateAllSelected()
        {
            var selectedCount = LibraryItems.Count(item => item.IsSelected);
            bool? value = LibraryItems.Count > 0 && selectedCount == LibraryItems.Count
                ? true
                : selectedCount == 0 ? false : null;
            if (isAllSelected == value)
            {
                return;
            }

            isAllSelected = value;
            OnPropertyChanged(nameof(IsAllSelected));
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("学习计划尚未初始化语言。");
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习计划语言值无效。");
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
