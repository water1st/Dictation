using System.Windows.Controls;
using System.Windows.Input;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Views
{
    /// <summary>
    /// 显示日英语词库查询结果和导入操作。
    /// </summary>
    public sealed partial class LexiconLookupPage : UserControl
    {
        /// <summary>
        /// 显示日英语词库查询结果和导入操作。
        /// </summary>
        public LexiconLookupPage()
        {
            InitializeComponent();
        }

        private void SearchComboBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is ComboBox { IsDropDownOpen: true })
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
