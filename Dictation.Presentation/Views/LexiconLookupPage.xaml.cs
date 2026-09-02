using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Views
{
    public sealed partial class LexiconLookupPage : System.Windows.Controls.UserControl
    {
        public LexiconLookupPage()
        {
            InitializeComponent();
        }

        private void SearchComboBoxPreviewKeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter)
            {
                return;
            }

            if (DataContext is LexiconLookupViewModel viewModel
                && viewModel.SearchCommand.CanExecute(null))
            {
                viewModel.SearchCommand.Execute(null);
            }

            e.Handled = true;
        }
    }
}
