namespace Dictation.Presentation.Views
{
    /// <summary>承载单窗口错题概览及页面内详情，全部交互通过ViewModel命令。</summary>
    public sealed partial class WrongAnswerListPage : System.Windows.Controls.UserControl
    {
        /// <summary>创建绑定控件，不执行业务或持久化。</summary>
        public WrongAnswerListPage() => InitializeComponent();
    }
}
