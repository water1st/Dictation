using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 呈现当前导入词组的成员详情与中文释义编辑状态。
    /// </summary>
    public sealed class ImportInboxDetailViewModel : INotifyPropertyChanged
    {
        private readonly IImportInboxActions importInboxActions;
        private LearningLanguage selectedLanguage;
        private ImportBatchDto? batch;
        private ImportBatchItemDto? item;
        private double scrollOffset;
        private bool isInitialized;

        /// <summary>
        /// 呈现当前导入词组的成员详情与中文释义编辑状态。
        /// </summary>
        /// <param name="importInboxActions">保存词组中文释义的业务能力。</param>
        public ImportInboxDetailViewModel(IImportInboxActions importInboxActions)
        {
            this.importInboxActions = importInboxActions
                ?? throw new ArgumentNullException(nameof(importInboxActions));
            Members = new ObservableCollection<ImportMemberEditorViewModel>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ImportMemberEditorViewModel> Members { get; }

        public LearningLanguage SelectedLanguage
        {
            get
            {
                EnsureInitialized();
                return selectedLanguage;
            }
        }

        public ImportBatchDto Batch => batch
            ?? throw new InvalidOperationException("导入详情尚未初始化批次。");

        public ImportBatchItemDto Item => item
            ?? throw new InvalidOperationException("导入详情尚未初始化项目。");

        public string WindowTitle => isInitialized
            ? selectedLanguage == LearningLanguage.Japanese ? "日语导入详情" : "英语导入详情"
            : "导入详情";

        public string LanguageLabel => selectedLanguage == LearningLanguage.Japanese ? "日语" : "英语";

        public string SourceText => Item.SourceText;

        public string DecisionLabel => Item.Decision switch
        {
            ImportItemDecision.Pending => "待确认",
            ImportItemDecision.Duplicate => "重复（已忽略）",
            ImportItemDecision.Learning => "已送入学习",
            ImportItemDecision.Review => "已送入复习",
            ImportItemDecision.Discard => "已丢弃",
            _ => Item.Decision.ToString()
        };

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

        /// <summary>
        /// 从给定批次选择条目，并构建仅供显示和编辑的成员状态。
        /// </summary>
        /// <param name="batch">包含目标条目及词组快照的导入批次。</param>
        /// <param name="itemId">批次内当前显示的条目标识。</param>
        public void Initialize(ImportBatchDto batch, long itemId)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
            ValidateLanguage(batch.Language);
            if (isInitialized)
            {
                throw new InvalidOperationException("导入详情已经初始化，不能重复初始化。");
            }

            var selectedItem = batch.Items.SingleOrDefault(candidate => candidate.ItemId == itemId)
                ?? throw new ArgumentException("指定项目不属于当前导入批次。", nameof(itemId));
            this.batch = batch;
            item = selectedItem;
            selectedLanguage = batch.Language;
            Members.Clear();
            if (selectedItem.WordGroup is not null)
            {
                foreach (var member in selectedItem.WordGroup.Members)
                {
                    Members.Add(new ImportMemberEditorViewModel(
                        member,
                        selectedItem.WordGroup.WordGroupId,
                        importInboxActions));
                }
            }

            isInitialized = true;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(Batch));
            OnPropertyChanged(nameof(Item));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(DecisionLabel));
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("导入详情尚未初始化。");
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "导入详情语言值无效。");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
