using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation
{
    public sealed partial class HomeWindow : System.Windows.Window
    {
        private readonly HomeViewModel viewModel;

        public HomeWindow(HomeViewModel viewModel)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = this.viewModel;
        }

        public HomeViewModel ViewModel => viewModel;
    }
}
