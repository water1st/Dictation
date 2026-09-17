using System.ComponentModel;
using Dictation.Core.Importing;
using Dictation.Core.Learning;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 承载学习库列表项的显示文本和勾选状态。
    /// </summary>
    public sealed class LearningLibraryItemViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        /// <summary>
        /// 承载学习库列表项的显示文本和勾选状态。
        /// </summary>
        /// <param name="group">学习库列表显示的词组快照。</param>
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
