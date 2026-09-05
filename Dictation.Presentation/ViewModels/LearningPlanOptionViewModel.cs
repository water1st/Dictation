using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dictation.Presentation.ViewModels
{
    public sealed class LearningPlanOptionViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

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
