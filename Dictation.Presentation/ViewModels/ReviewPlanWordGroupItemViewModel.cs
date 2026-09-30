using Dictation.Core.Review;
using System.ComponentModel;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>组合既有复习详情展示与计划范围勾选状态，不复制词库或风险逻辑。</summary>
    public sealed class ReviewPlanWordGroupItemViewModel : INotifyPropertyChanged
    {
        /// <summary>适配 Core 已装配的完整词组。</summary>
        /// <param name="value">当前语言的词组及记忆详情。</param>
        public ReviewPlanWordGroupItemViewModel(ReviewWordGroupDto value)
        {
            Details = new ReviewWordGroupItemViewModel(value);
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ReviewWordGroupItemViewModel Details { get; }
        public string WordGroupId => Details.WordGroupId;
        private bool isSelected;
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
