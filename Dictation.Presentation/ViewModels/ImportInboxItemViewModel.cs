using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        public string AnchorSourceSenseId => Item.AnchorSourceSenseId;

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

        public ImportWordGroupDto? WordGroup => Item.WordGroup;

        public ImportWordGroupMemberDto? PrimaryMember => WordGroup?.Members.FirstOrDefault();

        public bool IsSelectable => Decision == ImportItemDecision.Pending;

        public string SearchText => string.Join(
            " ",
            new[]
            {
                SourceText,
                AnchorSourceSenseId,
                WordGroup is null
                    ? string.Empty
                    : string.Join(
                        " ",
                        WordGroup.Members.SelectMany(member =>
                            new[]
                            {
                                member.Headword,
                                member.Reading ?? string.Empty,
                                member.PartOfSpeechLabel,
                                member.AttributeLabel,
                                string.Join(" ", member.Meanings)
                            }))
            });

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
}
