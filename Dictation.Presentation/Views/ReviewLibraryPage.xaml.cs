namespace Dictation.Presentation.Views
{
    /// <summary>在单窗口中承载复习词库卡片与详情；状态及命令通过 ViewModel 绑定。</summary>
    public sealed partial class ReviewLibraryPage : System.Windows.Controls.UserControl
    {
        /// <summary>创建可绑定的页面控件，不执行查询或业务规则。</summary>
        public ReviewLibraryPage() => InitializeComponent();
    }
}
