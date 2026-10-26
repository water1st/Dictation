namespace Dictation.Presentation.Views
{
    /// <summary>呈现固定语言的业务入口和进度。</summary>
    public sealed partial class LanguageWorkspacePage : System.Windows.Controls.UserControl
    {
        public LanguageWorkspacePage()
        {
            InitializeComponent();
            Unloaded += (_, _) =>
            {
                if (DataContext is Dictation.Presentation.ViewModels.HomeViewModel home)
                    home.DismissCheckInCelebration();
            };
        }
    }
}
