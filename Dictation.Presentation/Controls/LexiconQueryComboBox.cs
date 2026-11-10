using System.Windows;
using System.Windows.Controls;

namespace Dictation.Presentation.Controls
{
    /// <summary>候选用于查看义项，选中候选时保留用户原本输入的查词文本。</summary>
    public sealed class LexiconQueryComboBox : ComboBox
    {
        private bool isSynchronizingSelection;

        static LexiconQueryComboBox()
        {
            IsDropDownOpenProperty.OverrideMetadata(typeof(LexiconQueryComboBox), new FrameworkPropertyMetadata
            {
                CoerceValueCallback = RequireCandidates
            });
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            var query = Text;
            var wasSynchronizingSelection = isSynchronizingSelection;
            isSynchronizingSelection = true;
            try
            {
                base.OnSelectionChanged(e);
                SetCurrentValue(TextProperty, query);
            }
            finally
            {
                isSynchronizingSelection = wasSynchronizingSelection;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == TextProperty && !isSynchronizingSelection)
            {
                GetBindingExpression(TextProperty)?.UpdateSource();
            }
        }

        private static object RequireCandidates(DependencyObject sender, object value)
        {
            return ((LexiconQueryComboBox)sender).HasItems && (bool)value;
        }
    }
}
