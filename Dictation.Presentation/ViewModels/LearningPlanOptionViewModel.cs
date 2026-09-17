using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 承载一个学习计划选项的代码、显示标签和选择状态。
    /// </summary>
    public sealed class LearningPlanOptionViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        /// <summary>
        /// 承载一个学习计划选项的代码、显示标签和选择状态。
        /// </summary>
        /// <param name="code">持久化使用的选项代码。</param>
        /// <param name="label">用户可见的选项标签。</param>
        public LearningPlanOptionViewModel(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Code { get; }

        public string Label { get; }

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
