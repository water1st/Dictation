using System.Windows.Controls;

namespace Dictation.Presentation.Views
{
    /// <summary>展示月历和统计绑定，不创建窗口或直接查询数据库。</summary>
    public sealed partial class StatisticsPage : UserControl
    {
        /// <summary>由单窗口数据模板创建页面。</summary>
        public StatisticsPage() => InitializeComponent();
    }
}
