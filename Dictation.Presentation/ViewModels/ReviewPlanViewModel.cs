using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Core.Review;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>协调固定语言的复习计划与词组管理页面，业务校验和事务留在 Core。</summary>
    public sealed class ReviewPlanViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IReviewPlanReader reader;
        private readonly IReviewPlanEditor editor;
        private readonly IReviewLibraryQuery query;
        private readonly IReviewLibraryManager manager;
        private readonly ILexiconSearch lexicon;
        private readonly IOverlayRequester overlay;
        private readonly List<AsyncCommand> editCommands = new();
        private readonly AsyncCommand cancelCommand;
        private CancellationTokenSource? operationCancellation;
        private bool isInitialized;
        private bool isDisposed;
        private bool isBusy;
        private bool hasLoaded;
        private bool applyingSelection;
        private int dailyReviewTarget;
        private int sessionWordLimit;
        private ReviewRangeMode rangeMode;
        private EnglishFormLabelMode englishFormLabelMode;
        private ReviewPlanWordGroupItemViewModel? selectedGroup;
        private LexiconLookupResultDto? selectedExample;
        private string? exampleSourceSenseId;
        private string exampleQuery = string.Empty;
        private string exampleSummary = string.Empty;
        private string statusMessage = "尚未加载复习计划。";
        private string? errorMessage;

        /// <summary>组合页面需要的最小业务与导航能力。</summary>
        /// <param name="reader">独立复习计划读取。</param>
        /// <param name="editor">独立复习计划保存。</param>
        /// <param name="query">当前语言复习库查询。</param>
        /// <param name="manager">整组删除能力。</param>
        /// <param name="lexicon">示例动词的离线查询。</param>
        /// <param name="navigation">上级页面导航。</param>
        /// <param name="overlay">删除前的单窗口确认。</param>
        public ReviewPlanViewModel(IReviewPlanReader reader, IReviewPlanEditor editor, IReviewLibraryQuery query,
            IReviewLibraryManager manager, ILexiconSearch lexicon, IBackNavigation navigation, IOverlayRequester overlay)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));
            this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            ArgumentNullException.ThrowIfNull(navigation);
            SaveCommand = CreateEditCommand(() => SaveAsync());
            RefreshCommand = CreateEditCommand(() => RefreshAsync(), requireLoaded: false);
            SearchExamplesCommand = CreateEditCommand(() => SearchExamplesAsync());
            var deleteCommand = new AsyncCommand(() => DeleteSelectedGroupAsync(), () => CanEdit && SelectedGroup is not null);
            editCommands.Add(deleteCommand);
            DeleteSelectedCommand = deleteCommand;
            cancelCommand = new AsyncCommand(() => { CancelOperation(); return Task.CompletedTask; }, () => !isDisposed && IsBusy);
            BackCommand = new AsyncCommand(async () => { CancelOperation(); await navigation.GoBackAsync(); }, () => !isDisposed);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public LearningLanguage SelectedLanguage { get; private set; }
        public string WindowTitle => IsEnglish ? "英语复习计划" : "日语复习计划";
        public bool IsEnglish => isInitialized && SelectedLanguage == LearningLanguage.English;
        public bool CanEdit => isInitialized && hasLoaded && !IsBusy && !isDisposed;
        public bool CanSelectGroups => CanEdit && RangeMode == ReviewRangeMode.Selected;
        public bool CanSelectAll => CanSelectGroups && Groups.Count > 0;
        public int DailyReviewTarget { get => dailyReviewTarget; set => SetProperty(ref dailyReviewTarget, value); }
        public int SessionWordLimit { get => sessionWordLimit; set => SetProperty(ref sessionWordLimit, value); }
        public ReviewRangeMode RangeMode
        {
            get => rangeMode;
            set
            {
                if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (SetProperty(ref rangeMode, value))
                {
                    OnPropertyChanged(nameof(IsAllWords)); OnPropertyChanged(nameof(IsSelectedRange)); OnPropertyChanged(nameof(CanSelectGroups));
                    OnPropertyChanged(nameof(CanSelectAll));
                }
            }
        }
        public bool IsAllWords { get => RangeMode == ReviewRangeMode.All; set { if (value) RangeMode = ReviewRangeMode.All; } }
        public bool IsSelectedRange { get => RangeMode == ReviewRangeMode.Selected; set { if (value) RangeMode = ReviewRangeMode.Selected; } }
        public bool? IsAllSelected
        {
            get => Groups.Count == 0 || Groups.All(group => !group.IsSelected) ? false : Groups.All(group => group.IsSelected) ? true : null;
            set
            {
                if (value is null || IsBusy || isDisposed) return;
                applyingSelection = true;
                try { foreach (var group in Groups) group.IsSelected = value.Value; }
                finally { applyingSelection = false; OnPropertyChanged(); }
            }
        }
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (SetProperty(ref isBusy, value)) NotifyAvailability();
            }
        }
        public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
        public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
        public ObservableCollection<ReviewPlanWordGroupItemViewModel> Groups { get; } = new();
        public ReviewPlanWordGroupItemViewModel? SelectedGroup
        {
            get => selectedGroup;
            set
            {
                if (value is not null && !Groups.Contains(value)) throw new ArgumentException("请选择当前语言词库中的词组。", nameof(value));
                if (SetProperty(ref selectedGroup, value))
                {
                    ((AsyncCommand)DeleteSelectedCommand).NotifyCanExecuteChanged();
                }
            }
        }
        public ObservableCollection<LearningPlanOptionViewModel> FormOptions { get; } = new();
        public ObservableCollection<LearningPlanOptionViewModel> ChannelOptions { get; } = new();
        public ObservableCollection<LearningPlanOptionViewModel> OutputOptions { get; } = new();
        public ObservableCollection<LexiconLookupResultDto> ExampleCandidates { get; } = new();
        public LexiconLookupResultDto? SelectedExample
        {
            get => selectedExample;
            set
            {
                if (value is not null && !ExampleCandidates.Contains(value)) throw new ArgumentException("请选择检索结果中的示例动词。", nameof(value));
                if (SetProperty(ref selectedExample, value))
                {
                    exampleSourceSenseId = value?.SourceSenseId;
                    ExampleSummary = value is null ? "尚未选择示例动词。" : $"示例动词：{value.Headword}";
                }
            }
        }
        public string ExampleQuery { get => exampleQuery; set => SetProperty(ref exampleQuery, value); }
        public string ExampleSummary { get => exampleSummary; private set => SetProperty(ref exampleSummary, value); }
        public EnglishFormLabelMode EnglishFormLabelMode
        {
            get => englishFormLabelMode;
            set
            {
                if (SetProperty(ref englishFormLabelMode, value)) { OnPropertyChanged(nameof(IsGrammarName)); OnPropertyChanged(nameof(IsExampleParadigm)); }
            }
        }
        public bool IsGrammarName { get => EnglishFormLabelMode == EnglishFormLabelMode.GrammarName; set { if (value) EnglishFormLabelMode = EnglishFormLabelMode.GrammarName; } }
        public bool IsExampleParadigm { get => EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm; set { if (value) EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm; } }
        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand CancelCommand => cancelCommand;
        public ICommand DeleteSelectedCommand { get; }
        public ICommand SearchExamplesCommand { get; }

        /// <summary>由首页固定页面语言，不允许在计划中切换。</summary>
        /// <param name="language">当前语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
            if (isInitialized && SelectedLanguage != language) throw new InvalidOperationException("请返回首页后切换语言。");
            if (isInitialized) return;
            SelectedLanguage = language;
            isInitialized = true;
            foreach (var option in PracticePlanOptionFactory.CreateForms(language)) FormOptions.Add(option);
            foreach (var option in PracticePlanOptionFactory.CreateChannels()) ChannelOptions.Add(option);
            foreach (var option in PracticePlanOptionFactory.CreateOutputs(language)) OutputOptions.Add(option);
            OnPropertyChanged(nameof(WindowTitle)); OnPropertyChanged(nameof(IsEnglish)); NotifyAvailability();
        }

        /// <summary>在后台读取独立计划和复习库，完成后在原 UI 上下文应用。</summary>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default) => RunOperationAsync(async token =>
        {
            var result = await Task.Run(async () =>
            {
                var plan = await reader.LoadAsync(SelectedLanguage, token);
                var library = await query.LoadAsync(SelectedLanguage, token);
                return (plan, library);
            }, token);
            token.ThrowIfCancellationRequested();
            if (result.plan.Language != SelectedLanguage || result.library.Language != SelectedLanguage)
                throw new InvalidDataException("复习计划或词库的语言与当前页面不一致。");
            ApplyPlan(result.plan);
            ApplyGroups(result.library, result.plan.RangeMode == ReviewRangeMode.All ? null : result.plan.WordGroupIds);
            hasLoaded = true; NotifyAvailability();
            StatusMessage = $"共 {Groups.Count} 组 · 按风险优先复习；勾选范围不删除词条。";
        }, cancellationToken, "正在加载复习计划…");

        /// <summary>先在 UI 上取得选项快照，再经 Core 原子保存，取消或退场后不回写绑定。</summary>
        /// <param name="cancellationToken">取消保存的令牌。</param>
        public Task SaveAsync(CancellationToken cancellationToken = default) => RunOperationAsync(async token =>
        {
            var request = new SaveReviewPlanRequestDto(SelectedLanguage, DailyReviewTarget, SessionWordLimit, RangeMode,
                Groups.Where(group => group.IsSelected).Select(group => group.WordGroupId).ToArray(),
                FormOptions.Where(option => option.IsSelected).Select(option => option.Code).ToArray(),
                ChannelOptions.Where(option => option.IsSelected).Select(option => Enum.Parse<LearningChannel>(option.Code)).ToArray(),
                OutputOptions.Where(option => option.IsSelected).Select(option => Enum.Parse<LearningOutputMode>(option.Code)).ToArray(),
                EnglishFormLabelMode, IsEnglish ? exampleSourceSenseId : null);
            var saved = await Task.Run(() => editor.SaveAsync(request, token), token);
            token.ThrowIfCancellationRequested();
            if (saved.Language != SelectedLanguage) throw new InvalidDataException("保存响应的语言与页面不一致。");
            ApplyPlan(saved);
            StatusMessage = "复习计划已保存。";
        }, cancellationToken, "正在保存复习计划…");

        /// <summary>查询真实离线词库中的非短语动词，筛选能覆盖当前词形的候选。</summary>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        public Task SearchExamplesAsync(CancellationToken cancellationToken = default) => RunOperationAsync(async token =>
        {
            if (!IsEnglish) throw new InvalidOperationException("只有英语计划使用示例动词。");
            var forms = FormOptions.Where(option => option.IsSelected).Select(option => option.Code).ToArray();
            var request = new LexiconSearchRequestDto(LearningLanguage.English, ExampleQuery, 20);
            var result = await Task.Run(() => lexicon.SearchAsync(request, token), token);
            token.ThrowIfCancellationRequested();
            SelectedExample = null;
            ExampleCandidates.Clear();
            foreach (var item in result.Where(item => item.PartOfSpeechLabel == "动词" && !item.IsPhrase
                && forms.All(code => item.Forms.Any(form => form.FormCode == code)))) ExampleCandidates.Add(item);
            StatusMessage = ExampleCandidates.Count == 0 ? "没有覆盖当前词形的示例动词。" : "请选择示例动词的义项。";
        }, cancellationToken, "正在检索英语示例动词…");

        /// <summary>经单窗口弹层确认后物理删除详情中选中的整组，保留未保存的其它计划编辑。</summary>
        /// <param name="cancellationToken">取消确认或删除的令牌。</param>
        public Task DeleteSelectedGroupAsync(CancellationToken cancellationToken = default) => RunOperationAsync(async token =>
        {
            var group = SelectedGroup ?? throw new ArgumentException("请先选中要删除的词组。");
            var confirmed = await overlay.ConfirmAsync("删除复习词组", $"确定删除“{group.Details.HeadwordLabel}”整组及其学习进度、记忆和答题历史吗？此操作不能撤销。", token);
            token.ThrowIfCancellationRequested();
            if (!confirmed) { StatusMessage = "已取消删除。"; return; }
            var selectedIds = Groups.Where(item => item.IsSelected && item.WordGroupId != group.WordGroupId).Select(item => item.WordGroupId).ToArray();
            var library = await Task.Run(async () =>
            {
                await manager.DeleteWordGroupAsync(SelectedLanguage, group.WordGroupId, token);
                return await query.LoadAsync(SelectedLanguage, token);
            }, token);
            token.ThrowIfCancellationRequested();
            if (library.Language != SelectedLanguage) throw new InvalidDataException("删除后词库的语言与页面不一致。");
            ApplyGroups(library, RangeMode == ReviewRangeMode.All ? null : selectedIds);
            StatusMessage = $"已删除整组及相关进度，共 {Groups.Count} 组。";
        }, cancellationToken, "正在处理整组删除…");

        /// <summary>返回开始时立即取消操作，不等退场动画释放作用域。</summary>
        public void CancelOperation() => operationCancellation?.Cancel();

        /// <summary>取消页面操作、移除项目订阅，并停止全部旧页通知。</summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            CancelOperation();
            foreach (var group in Groups) group.PropertyChanged -= GroupSelectionChanged;
        }

        /// <summary>统一页面操作的互斥、错误提示、取消与退场保护，不在 UI 上执行数据库查询。</summary>
        /// <param name="operation">在 UI 上下文编排、内部将耗时业务放到后台的操作。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <param name="status">开始时显示的操作状态。</param>
        private async Task RunOperationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken, string status)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!isInitialized) throw new InvalidOperationException("复习计划尚未初始化。");
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy) return;
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCancellation = source;
            IsBusy = true; ErrorMessage = null; StatusMessage = status;
            try { await operation(source.Token); }
            catch (OperationCanceledException) when (source.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (!isDisposed) StatusMessage = "已取消操作，可重新加载。";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                if (!isDisposed && !source.IsCancellationRequested) { ErrorMessage = exception.Message; StatusMessage = "操作未完成，请检查后重试。"; }
            }
            finally
            {
                operationCancellation = null;
                if (!isDisposed) IsBusy = false;
            }
        }

        /// <summary>将返回的规范化计划同步到绑定选项，不改用户记忆。</summary>
        /// <param name="plan">与页面语言一致的独立复习计划。</param>
        private void ApplyPlan(ReviewPlanDto plan)
        {
            DailyReviewTarget = plan.DailyReviewTarget; SessionWordLimit = plan.SessionWordLimit; RangeMode = plan.RangeMode;
            EnglishFormLabelMode = plan.EnglishFormLabelMode;
            exampleSourceSenseId = plan.EnglishExampleSourceSenseId;
            selectedExample = ExampleCandidates.FirstOrDefault(item => item.SourceSenseId == exampleSourceSenseId);
            OnPropertyChanged(nameof(SelectedExample));
            ExampleSummary = plan.EnglishExampleParadigm is null ? "尚未选择示例动词。"
                : $"示例：{plan.EnglishExampleParadigm.Headword} — {string.Join(" / ", plan.EnglishExampleParadigm.Forms.Select(form => form.Surface))}";
            ApplyOptions(FormOptions, plan.FormCodes);
            ApplyOptions(ChannelOptions, plan.Channels.Select(value => value.ToString()));
            ApplyOptions(OutputOptions, plan.OutputModes.Select(value => value.ToString()));
        }

        /// <summary>替换当前语言的词组展示，按保存范围或当前编辑范围恢复勾选。</summary>
        /// <param name="library">Core 提供的词库与风险结果。</param>
        /// <param name="selectedIds">指定词组标识；空引用代表全部范围。</param>
        private void ApplyGroups(ReviewLibraryDto library, IReadOnlyList<string>? selectedIds)
        {
            var activeId = SelectedGroup?.WordGroupId;
            foreach (var group in Groups) group.PropertyChanged -= GroupSelectionChanged;
            SelectedGroup = null; Groups.Clear();
            var selected = selectedIds?.ToHashSet(StringComparer.Ordinal);
            foreach (var value in library.Groups)
            {
                var item = new ReviewPlanWordGroupItemViewModel(value) { IsSelected = selected is null || selected.Contains(value.WordGroup.WordGroupId) };
                item.PropertyChanged += GroupSelectionChanged;
                Groups.Add(item);
            }
            SelectedGroup = Groups.FirstOrDefault(item => item.WordGroupId == activeId);
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(CanSelectAll));
        }

        /// <summary>依据规范化代码更新选项勾选，不改变选项集合或业务规则。</summary>
        /// <param name="options">页面当前语言的可绑定选项。</param>
        /// <param name="codes">业务返回的已选代码。</param>
        private static void ApplyOptions(IEnumerable<LearningPlanOptionViewModel> options, IEnumerable<string> codes)
        {
            var selected = codes.ToHashSet(StringComparer.Ordinal);
            foreach (var option in options) option.IsSelected = selected.Contains(option.Code);
        }

        /// <summary>响应单组勾选并更新全选的三态显示，批量操作仅发送一次通知。</summary>
        /// <param name="sender">发生勾选变化的词组。</param>
        /// <param name="args">标识变更属性的通知。</param>
        private void GroupSelectionChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (!applyingSelection && args.PropertyName == nameof(ReviewPlanWordGroupItemViewModel.IsSelected)) OnPropertyChanged(nameof(IsAllSelected));
        }

        /// <summary>创建遵守页面生命周期的编辑命令，不额外创建命令框架。</summary>
        /// <param name="execute">页面的异步操作。</param>
        /// <param name="requireLoaded">保存与编辑需要已加载计划，刷新本身不需要。</param>
        /// <returns>注册到当前页面可用性通知中的命令。</returns>
        private AsyncCommand CreateEditCommand(Func<Task> execute, bool requireLoaded = true)
        {
            var command = new AsyncCommand(execute, () => isInitialized && !IsBusy && !isDisposed && (!requireLoaded || hasLoaded));
            editCommands.Add(command);
            return command;
        }

        /// <summary>同步编辑区、范围选择与命令可用状态。</summary>
        private void NotifyAvailability()
        {
            OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanSelectGroups));
            OnPropertyChanged(nameof(CanSelectAll));
            foreach (var command in editCommands) command.NotifyCanExecuteChanged();
            cancelCommand.NotifyCanExecuteChanged();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }

        /// <summary>页面退场后不再发送绑定通知，避免迟到结果影响旧页面。</summary>
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            if (!isDisposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
