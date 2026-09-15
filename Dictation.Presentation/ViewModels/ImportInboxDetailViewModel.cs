using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;

namespace Dictation.Presentation.ViewModels
{
    public sealed class ImportInboxDetailViewModel : INotifyPropertyChanged
    {
        private LearningLanguage selectedLanguage;
        private ImportBatchDto? batch;
        private ImportBatchItemDto? item;
        private double scrollOffset;
        private bool isInitialized;

        public ImportInboxDetailViewModel()
        {
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
                    Members.Add(new ImportMemberEditorViewModel(member));
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
