using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        private void SearchComboBoxDropDownOpened(object? sender, EventArgs e)
        {
            UpdateCandidatePopupWidth();
        }

        private void LookupPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCandidatePopupWidth();
        }

        private void UpdateCandidatePopupWidth()
        {
            if (QueryComboBox is null || CandidatePane is null || !QueryComboBox.IsDropDownOpen
                || QueryComboBox.Template.FindName("PART_Popup", QueryComboBox) is not Popup { Child: FrameworkElement content })
            {
                return;
            }

            // 输入框填充整行，候选弹层仍留在左侧结果区，保留右侧导入操作的点击空间。
            var right = CandidatePane.TranslatePoint(new Point(CandidatePane.ActualWidth, 0), this).X;
            var left = QueryComboBox.TranslatePoint(new Point(), this).X;
            content.MaxWidth = Math.Max(0, right - left);
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
