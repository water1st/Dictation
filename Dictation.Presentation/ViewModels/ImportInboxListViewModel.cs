using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 管理导入收件箱列表、筛选、选择和批量处理交互。
    /// </summary>
    public sealed class ImportInboxListViewModel : INotifyPropertyChanged
    {
        private readonly IImportInboxQuery importInboxQuery;
        private readonly IImportInboxActions importInboxActions;
        private readonly NavigationService navigationService;
        private readonly OverlayService overlayService;
        private readonly List<ImportInboxItemViewModel> allItems = new();
        private readonly Dictionary<string, HashSet<long>> selectedItemIdsByBatch =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> selectedItemIdsByBatchItem =
            new(StringComparer.Ordinal);
        private readonly SemaphoreSlim operationGate = new(1, 1);
        private LearningLanguage selectedLanguage;
        private ImportBatchDto? selectedBatch;
        private ImportInboxItemViewModel? selectedItem;
        private bool? isAllSelected;
        private string filterText = string.Empty;
        private string? errorMessage;
        private string emptyStateText = "当前语言没有待确认导入。";
        private double scrollOffset;
        private bool isInitialized;

        /// <summary>
        /// 初始化导入收件箱列表视图模型并创建页面命令。
        /// </summary>
        /// <param name="importInboxQuery">用于读取待处理批次的查询能力。</param>
        /// <param name="importInboxActions">用于确认项目和维护释义的操作能力。</param>
        /// <param name="navigationService">用于页面导航的服务。</param>
        /// <param name="overlayService">用于确认和错误消息的覆盖层服务。</param>
        public ImportInboxListViewModel(
            IImportInboxQuery importInboxQuery,
            IImportInboxActions importInboxActions,
            NavigationService navigationService,
            OverlayService overlayService)
        {
            this.importInboxQuery = importInboxQuery
                ?? throw new ArgumentNullException(nameof(importInboxQuery));
            this.importInboxActions = importInboxActions
                ?? throw new ArgumentNullException(nameof(importInboxActions));
            this.navigationService = navigationService
                ?? throw new ArgumentNullException(nameof(navigationService));
            this.overlayService = overlayService
                ?? throw new ArgumentNullException(nameof(overlayService));
            Batches = new ObservableCollection<ImportBatchDto>();
            Items = new ObservableCollection<ImportInboxItemViewModel>();
            BackCommand = new AsyncCommand(() => this.navigationService.GoBackAsync());
            RefreshCommand = new AsyncCommand(() => RefreshAsync());
            OpenSelectedDetailCommand = new AsyncCommand(() => OpenSelectedDetailAsync());
            SendToLearningCommand = new AsyncCommand(() => DecideAsync(ImportDecision.Learning));
            SendToReviewCommand = new AsyncCommand(() => DecideAsync(ImportDecision.Review));
            DiscardCommand = new AsyncCommand(() => DecideAsync(ImportDecision.Discard));
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

        public string WindowTitle => isInitialized
            ? selectedLanguage == LearningLanguage.Japanese ? "日语导入收件箱" : "英语导入收件箱"
            : "导入收件箱";

        public string LanguageLabel => selectedLanguage == LearningLanguage.Japanese ? "日语" : "英语";

        public ObservableCollection<ImportBatchDto> Batches { get; }

        public ObservableCollection<ImportInboxItemViewModel> Items { get; }

        public ImportBatchDto? SelectedBatch
        {
            get => selectedBatch;
            set
            {
                if (ReferenceEquals(selectedBatch, value))
                {
                    return;
                }

                if (value is not null
                    && !Batches.Any(batch => string.Equals(batch.BatchId, value.BatchId, StringComparison.Ordinal)))
                {
                    ErrorMessage = "请选择当前列表中的批次。";
                    return;
                }

                SaveCurrentSelection();
                selectedBatch = value;
                OnPropertyChanged();
                LoadItems(value);
            }
        }

        public ImportInboxItemViewModel? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (ReferenceEquals(selectedItem, value))
                {
                    return;
                }

                if (value is not null && !allItems.Contains(value))
                {
                    ErrorMessage = "请选择当前批次中的项目。";
                    return;
                }

                selectedItem = value;
                if (selectedBatch is not null && value is not null)
                {
                    selectedItemIdsByBatchItem[selectedBatch.BatchId] = value.ItemId;
                }

                OnPropertyChanged();
            }
        }

        public string FilterText
        {
            get => filterText;
            set
            {
                var nextValue = value ?? string.Empty;
                if (string.Equals(filterText, nextValue, StringComparison.Ordinal))
                {
                    return;
                }

                filterText = nextValue;
                OnPropertyChanged();
                RebuildVisibleItems();
            }
        }

        public bool? IsAllSelected
        {
            get => isAllSelected;
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                foreach (var item in allItems.Where(item => item.IsSelectable))
                {
                    item.IsSelected = value.Value;
                }

                RecalculateSelectionState();
            }
        }

        public double ScrollOffset
        {
            get => scrollOffset;
            set
            {
                var nextValue = double.IsNaN(value) || double.IsInfinity(value)
                    ? 0
                    : Math.Max(0, value);
                if (scrollOffset.Equals(nextValue))
                {
                    return;
                }

                scrollOffset = nextValue;
                OnPropertyChanged();
            }
        }

        public bool CanDecide => allItems.Any(item => item.IsSelectable && item.IsSelected);

        public string? ErrorMessage
        {
            get => errorMessage;
            private set
            {
                if (string.Equals(errorMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string EmptyStateText
        {
            get => emptyStateText;
            private set
            {
                if (string.Equals(emptyStateText, value, StringComparison.Ordinal))
                {
                    return;
                }

                emptyStateText = value;
                OnPropertyChanged();
            }
        }

        public ICommand BackCommand { get; }

        public ICommand RefreshCommand { get; }

        public ICommand OpenSelectedDetailCommand { get; }

        public ICommand SendToLearningCommand { get; }

        public ICommand SendToReviewCommand { get; }

        public ICommand DiscardCommand { get; }

        /// <summary>
        /// 为收件箱页面绑定唯一的学习语言；初始化后不允许切换语言。
        /// </summary>
        /// <param name="language">本页面要显示的学习语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ValidateLanguage(language);
            if (isInitialized)
            {
                throw new InvalidOperationException("导入收件箱已经初始化，不能重复初始化语言。");
            }

            selectedLanguage = language;
            isInitialized = true;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LanguageLabel));
        }

        /// <summary>
        /// 重新读取当前语言的待处理批次。
        /// </summary>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RefreshCoreAsync(cancellationToken);
        }

        /// <summary>
        /// 重新读取批次，同时尽量保留当前批次、项目、筛选与滚动状态。
        /// </summary>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        public Task RefreshPreservingStateAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return RefreshCoreAsync(cancellationToken);
        }

        /// <summary>
        /// 打开当前所选批次项目的详情页。
        /// </summary>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public async Task OpenSelectedDetailAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            await operationGate.WaitAsync(cancellationToken);
            try
            {
                if (selectedBatch is null)
                {
                    ErrorMessage = "请选择一个待处理批次。";
                    return;
                }

                if (selectedItem is null || !allItems.Contains(selectedItem))
                {
                    ErrorMessage = "请选择一个批次项目。";
                    return;
                }

                await navigationService.OpenImportInboxDetailAsync(
                    selectedBatch,
                    selectedItem.ItemId,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"打开导入详情失败：{exception.Message}";
                await ShowErrorAsync("打开导入详情失败", exception.Message, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                operationGate.Release();
            }
        }

        /// <summary>
        /// 对当前批次内所有选中的待处理项目执行同一决定，并刷新列表。
        /// </summary>
        /// <param name="decision">学习、复习或丢弃决定。</param>
        /// <param name="cancellationToken">取消确认或刷新操作的令牌。</param>
        public async Task DecideAsync(
            ImportDecision decision,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(decision))
            {
                ErrorMessage = "导入决定无效，请选择学习、复习或丢弃。";
                return;
            }

            await operationGate.WaitAsync(cancellationToken);
            try
            {
                ErrorMessage = null;
                if (selectedBatch is null)
                {
                    ErrorMessage = "请选择一个待处理批次。";
                    return;
                }

                var itemIds = allItems
                    .Where(item => item.IsSelectable && item.IsSelected)
                    .Select(item => item.ItemId)
                    .Distinct()
                    .ToArray();
                if (itemIds.Length == 0)
                {
                    ErrorMessage = "请至少选择一个待处理项目。";
                    return;
                }

                if (decision == ImportDecision.Discard)
                {
                    var confirmed = await overlayService.ConfirmAsync(
                        "丢弃导入",
                        $"确定丢弃已选的 {itemIds.Length} 个项目吗？",
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!confirmed)
                    {
                        return;
                    }
                }

                await importInboxActions.DecideAsync(
                    new DecideImportBatchRequestDto(selectedBatch.BatchId, itemIds, decision),
                    cancellationToken);
                await RefreshCoreWithoutGateAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"处理导入收件箱失败：{exception.Message}";
                await ShowErrorAsync("处理导入收件箱失败", exception.Message, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                operationGate.Release();
            }
        }

        private async Task RefreshCoreAsync(CancellationToken cancellationToken)
        {
            await operationGate.WaitAsync(cancellationToken);
            try
            {
                await RefreshCoreWithoutGateAsync(cancellationToken);
            }
            finally
            {
                operationGate.Release();
            }
        }

        private async Task RefreshCoreWithoutGateAsync(CancellationToken cancellationToken)
        {
            try
            {
                ErrorMessage = null;
                var batches = await importInboxQuery.GetPendingBatchesAsync(
                    selectedLanguage,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyBatches(batches);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                EmptyStateText = "暂时无法显示导入收件箱。";
                ErrorMessage = $"收件箱加载失败：{exception.Message}";
                await ShowErrorAsync("收件箱加载失败", exception.Message, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void ApplyBatches(IReadOnlyList<ImportBatchDto> batches)
        {
            var previousBatchId = selectedBatch?.BatchId;
            var previousItemId = selectedItem?.ItemId;
            SaveCurrentSelection();

            Batches.Clear();
            foreach (var batch in batches)
            {
                Batches.Add(batch);
            }

            PruneSelectionState();
            var nextBatch = Batches.FirstOrDefault(batch =>
                string.Equals(batch.BatchId, previousBatchId, StringComparison.Ordinal))
                ?? Batches.FirstOrDefault();

            selectedBatch = null;
            OnPropertyChanged(nameof(SelectedBatch));
            LoadItems(nextBatch, previousItemId);
            selectedBatch = nextBatch;
            OnPropertyChanged(nameof(SelectedBatch));

            if (nextBatch is null)
            {
                ClearCurrentItems();
                EmptyStateText = "当前语言没有待确认导入。";
            }
            else
            {
                EmptyStateText = string.Empty;
            }
        }

        private void LoadItems(ImportBatchDto? batch, long? preferredItemId = null)
        {
            foreach (var item in allItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            allItems.Clear();
            Items.Clear();
            selectedItem = null;
            if (batch is not null)
            {
                foreach (var item in batch.Items)
                {
                    var viewModel = new ImportInboxItemViewModel(batch, item);
                    viewModel.PropertyChanged += OnItemPropertyChanged;
                    allItems.Add(viewModel);
                }

                if (selectedItemIdsByBatch.TryGetValue(batch.BatchId, out var selectedIds))
                {
                    foreach (var item in allItems)
                    {
                        item.IsSelected = item.IsSelectable && selectedIds.Contains(item.ItemId);
                    }
                }

                selectedItem = preferredItemId is not null
                    ? allItems.FirstOrDefault(item => item.ItemId == preferredItemId.Value)
                    : null;
                selectedItem ??= selectedItemIdsByBatchItem.TryGetValue(batch.BatchId, out var selectedId)
                    ? allItems.FirstOrDefault(item => item.ItemId == selectedId)
                    : null;
                selectedItem ??= allItems.FirstOrDefault(item => item.IsSelectable)
                    ?? allItems.FirstOrDefault();
            }

            RebuildVisibleItems();
            OnPropertyChanged(nameof(SelectedItem));
            RecalculateSelectionState();
            OnPropertyChanged(nameof(CanDecide));
        }

        private void ClearCurrentItems()
        {
            foreach (var item in allItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            allItems.Clear();
            Items.Clear();
            if (selectedItem is not null)
            {
                selectedItem = null;
                OnPropertyChanged(nameof(SelectedItem));
            }

            SetIsAllSelected(false);
            OnPropertyChanged(nameof(CanDecide));
        }

        private void RebuildVisibleItems()
        {
            Items.Clear();
            var query = filterText.Trim();
            foreach (var item in allItems)
            {
                if (query.Length == 0
                    || item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    Items.Add(item);
                }
            }

            RecalculateSelectionState();
            OnPropertyChanged(nameof(CanDecide));
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ImportInboxItemViewModel item
                && e.PropertyName == nameof(ImportInboxItemViewModel.IsSelected)
                && selectedBatch is not null)
            {
                var selectedIds = GetSelectedIds(selectedBatch.BatchId);
                if (item.IsSelected)
                {
                    selectedIds.Add(item.ItemId);
                }
                else
                {
                    selectedIds.Remove(item.ItemId);
                }
            }

            if (e.PropertyName == nameof(ImportInboxItemViewModel.IsSelected))
            {
                RecalculateSelectionState();
                OnPropertyChanged(nameof(CanDecide));
            }
        }

        private void RecalculateSelectionState()
        {
            var selectableItems = allItems.Where(item => item.IsSelectable).ToArray();
            var selectedCount = selectableItems.Count(item => item.IsSelected);
            SetIsAllSelected(selectableItems.Length > 0 && selectedCount == selectableItems.Length
                ? true
                : selectedCount == 0 ? false : null);
        }

        private void SetIsAllSelected(bool? value)
        {
            if (isAllSelected == value)
            {
                return;
            }

            isAllSelected = value;
            OnPropertyChanged(nameof(IsAllSelected));
        }

        private void SaveCurrentSelection()
        {
            if (selectedBatch is null)
            {
                return;
            }

            var selectedIds = GetSelectedIds(selectedBatch.BatchId);
            selectedIds.Clear();
            foreach (var item in allItems.Where(item => item.IsSelectable && item.IsSelected))
            {
                selectedIds.Add(item.ItemId);
            }

            if (selectedItem is not null)
            {
                selectedItemIdsByBatchItem[selectedBatch.BatchId] = selectedItem.ItemId;
            }
        }

        private HashSet<long> GetSelectedIds(string batchId)
        {
            if (!selectedItemIdsByBatch.TryGetValue(batchId, out var selectedIds))
            {
                selectedIds = new HashSet<long>();
                selectedItemIdsByBatch[batchId] = selectedIds;
            }

            return selectedIds;
        }

        private void PruneSelectionState()
        {
            var validBatches = Batches.ToDictionary(batch => batch.BatchId, StringComparer.Ordinal);
            foreach (var batchId in selectedItemIdsByBatch.Keys.ToArray())
            {
                if (!validBatches.TryGetValue(batchId, out var batch))
                {
                    selectedItemIdsByBatch.Remove(batchId);
                    selectedItemIdsByBatchItem.Remove(batchId);
                    continue;
                }

                var validIds = batch.Items
                    .Where(item => item.Decision == ImportItemDecision.Pending)
                    .Select(item => item.ItemId)
                    .ToHashSet();
                selectedItemIdsByBatch[batchId].IntersectWith(validIds);
                if (selectedItemIdsByBatchItem.TryGetValue(batchId, out var selectedItemId)
                    && !batch.Items.Any(item => item.ItemId == selectedItemId))
                {
                    selectedItemIdsByBatchItem.Remove(batchId);
                }
            }
        }

        private async Task ShowErrorAsync(
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            await overlayService.ShowMessageAsync(title, message, cancellationToken);
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("导入收件箱尚未初始化语言。");
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "导入收件箱语言值无效。");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
