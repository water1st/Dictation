using Dictation.Presentation.ViewModels;
using System.Windows.Input;

namespace Dictation.Presentation
{
    public sealed partial class LexiconLookupWindow : System.Windows.Window
    {
        private readonly LexiconLookupViewModel viewModel;

        public LexiconLookupWindow(LexiconLookupViewModel viewModel)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = this.viewModel;
        }

        public LexiconLookupViewModel ViewModel => viewModel;

        private void SearchComboBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (viewModel.SearchCommand.CanExecute(null))
            {
                viewModel.SearchCommand.Execute(null);
            }

            e.Handled = true;
        }
    }
}
