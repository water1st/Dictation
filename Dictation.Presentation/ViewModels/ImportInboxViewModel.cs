using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;

namespace Dictation.Presentation.ViewModels
{
    public sealed class ImportInboxItemViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public ImportInboxItemViewModel(ImportBatchDto batch, ImportBatchItemDto item)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImportBatchDto Batch { get; }

        public ImportBatchItemDto Item { get; }

        public long ItemId => Item.ItemId;

        public string SourceText => Item.SourceText;

        public ImportItemDecision Decision => Item.Decision;

        public string DecisionLabel => Decision switch
        {
            ImportItemDecision.Pending => "待确认",
            ImportItemDecision.Duplicate => "重复（已忽略）",
            ImportItemDecision.Learning => "已送入学习",
            ImportItemDecision.Review => "已送入复习",
            ImportItemDecision.Discard => "已丢弃",
            _ => Decision.ToString()
        };

        public string AnchorSourceSenseId => Item.AnchorSourceSenseId;

        public ImportWordGroupDto? WordGroup => Item.WordGroup;

        public bool IsSelectable => Decision == ImportItemDecision.Pending;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                var nextValue = IsSelectable && value;
                if (isSelected == nextValue)
                {
                    return;
                }

                isSelected = nextValue;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ImportInboxViewModel : INotifyPropertyChanged
    {
        private readonly ImportInboxService importInboxService;
        private LearningLanguage selectedLanguage;
        private ImportBatchDto? selectedBatch;
        private ImportInboxItemViewModel? selectedItem;
        private bool? isAllSelected;
        private string? errorMessage;
        private string emptyStateText = "当前语言没有待确认导入。";
        private bool isInitialized;

        public ImportInboxViewModel(ImportInboxService importInboxService)
        {
            this.importInboxService = importInboxService
                ?? throw new ArgumentNullException(nameof(importInboxService));
            Batches = new ObservableCollection<ImportBatchDto>();
            Items = new ObservableCollection<ImportInboxItemViewModel>();
            SendToLearningCommand = new AsyncCommand(async () =>
            {
                await DecideAsync(ImportDecision.Learning);
            });
            SendToReviewCommand = new AsyncCommand(async () =>
            {
                await DecideAsync(ImportDecision.Review);
            });
            DiscardCommand = new AsyncCommand(async () =>
            {
                await DecideAsync(ImportDecision.Discard);
            });
            RefreshCommand = new AsyncCommand(async () =>
            {
                await RefreshAsync();
            });
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

                selectedItem = value;
                OnPropertyChanged();
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

                foreach (var item in Items)
                {
                    if (item.IsSelectable)
                    {
                        item.IsSelected = value.Value;
                    }
                }

                RecalculateSelectionState();
            }
        }

        public bool CanDecide => Items.Any(item => item.IsSelectable && item.IsSelected);

        public ICommand SendToLearningCommand { get; }

        public ICommand SendToReviewCommand { get; }

        public ICommand DiscardCommand { get; }

        public ICommand RefreshCommand { get; }

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

        public string EmptyStateText
        {
            get => emptyStateText;
            private set
            {
                if (emptyStateText == value)
                {
                    return;
                }

                emptyStateText = value;
                OnPropertyChanged();
            }
        }

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
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            var previousBatchId = selectedBatch?.BatchId;
            ErrorMessage = null;
            ClearResults();

            try
            {
                var batches = await importInboxService.GetPendingBatchesAsync(
                    selectedLanguage,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var batch in batches)
                {
                    Batches.Add(batch);
                }

                if (Batches.Count == 0)
                {
                    EmptyStateText = "当前语言没有待确认导入。";
                    return;
                }

                EmptyStateText = string.Empty;
                SelectedBatch = Batches.FirstOrDefault(batch =>
                    string.Equals(batch.BatchId, previousBatchId, StringComparison.Ordinal))
                    ?? Batches[0];
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                EmptyStateText = "暂时无法显示导入收件箱。";
                ErrorMessage = $"收件箱加载失败：{exception.Message}";
            }
        }

        public async Task DecideAsync(
            ImportDecision decision,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ErrorMessage = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedBatch is null)
            {
                ErrorMessage = "请选择一个待处理批次。";
                return;
            }

            var itemIds = Items
                .Where(item => item.IsSelectable && item.IsSelected)
                .Select(item => item.ItemId)
                .ToArray();
            if (itemIds.Length == 0)
            {
                ErrorMessage = "请至少选择一个待处理项目。";
                return;
            }

            try
            {
                await importInboxService.DecideAsync(
                    new DecideImportBatchRequestDto(selectedBatch.BatchId, itemIds, decision),
                    cancellationToken);
                await RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"处理导入收件箱失败：{exception.Message}";
            }
        }

        private void LoadItems(ImportBatchDto? batch)
        {
            foreach (var item in Items)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            Items.Clear();
            if (batch is not null)
            {
                foreach (var item in batch.Items)
                {
                    var viewModel = new ImportInboxItemViewModel(batch, item);
                    viewModel.PropertyChanged += OnItemPropertyChanged;
                    Items.Add(viewModel);
                }
            }

            SelectedItem = Items.FirstOrDefault(item => item.IsSelectable) ?? Items.FirstOrDefault();
            RecalculateSelectionState();
            OnPropertyChanged(nameof(CanDecide));
        }

        private void ClearResults()
        {
            SelectedBatch = null;
            Batches.Clear();
            Items.Clear();
            SelectedItem = null;
            SetIsAllSelected(false);
            EmptyStateText = string.Empty;
            OnPropertyChanged(nameof(CanDecide));
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImportInboxItemViewModel.IsSelected))
            {
                RecalculateSelectionState();
                OnPropertyChanged(nameof(CanDecide));
            }
        }

        private void RecalculateSelectionState()
        {
            var selectableItems = Items.Where(item => item.IsSelectable).ToArray();
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
