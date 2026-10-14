using System.Windows;
using Dictation.Presentation.ViewModels;
using Microsoft.Win32;

namespace Dictation.Presentation.Views
{
    /// <summary>在单窗口中承载文本词表的输入和来源义项确认。</summary>
    public sealed partial class TextImportPage : System.Windows.Controls.UserControl
    {
        /// <summary>创建页面控件；页面状态通过数据模板绑定。</summary>
        public TextImportPage()
        {
            InitializeComponent();
        }

        /// <summary>从拖放数据中识别唯一 TXT 文件，不读取文件内容。</summary>
        /// <param name="data">WPF 拖放数据。</param>
        /// <returns>有效的单个 TXT 路径，否则为空。</returns>
        internal static string? GetDroppedTextFile(IDataObject data)
        {
            return data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths
                && string.Equals(Path.GetExtension(paths[0]), ".txt", StringComparison.OrdinalIgnoreCase)
                ? paths[0] : null;
        }

        /// <summary>仅由平台文件选择器提供路径，内容读取与候选状态仍由页面输入流程负责。</summary>
        private async void SelectFileClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TextImportViewModel { CanEdit: true } viewModel)
            {
                return;
            }
            var dialog = new OpenFileDialog { Filter = "UTF-8 词表 (*.txt)|*.txt", Multiselect = false, CheckFileExists = true };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                await viewModel.LoadFileAsync(dialog.FileName);
            }
        }

        /// <summary>只接受单个 TXT 文件，避免多个文件被静默取第一份。</summary>
        private void PreviewFileDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DataContext is TextImportViewModel { CanEdit: true } && GetDroppedTextFile(e.Data) is not null
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>把拖入的文件转入同一候选确认流程，不直接暂存或确认词组。</summary>
        private async void FileDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (DataContext is not TextImportViewModel { CanEdit: true } viewModel)
            {
                return;
            }
            var path = GetDroppedTextFile(e.Data);
            if (path is null)
            {
                viewModel.ReportInputError("请一次拖入一个 UTF-8 TXT 文件。");
                return;
            }
            await viewModel.LoadFileAsync(path);
        }
    }
}
