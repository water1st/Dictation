using System.ComponentModel;
using Dictation.Services.Importing;
using Dictation.Services.Learning;

namespace Dictation.Presentation.ViewModels
{
    public sealed class LearningLibraryItemViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public LearningLibraryItemViewModel(LearningWordGroupDto group)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LearningWordGroupDto Group { get; }

        public string WordGroupId => Group.WordGroupId;

        public ImportWordGroupMemberDto? PrimaryMember => Group.Members.FirstOrDefault();

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
}
