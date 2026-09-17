using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Views
{
    /// <summary>
    /// 显示日英语词库查询结果和导入操作。
    /// </summary>
    public sealed partial class LexiconLookupPage : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// 显示日英语词库查询结果和导入操作。
        /// </summary>
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
