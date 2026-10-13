using System.Windows;
using System.Windows.Controls;
using Dictation.Core.Configuration;

namespace Dictation.Presentation.Views
{
    /// <summary>绘制圆形日英语言徽章，不依赖系统国旗字体或业务服务。</summary>
    public sealed partial class LanguageBadge : UserControl
    {
        public static readonly DependencyProperty SourceLanguageProperty = DependencyProperty.Register(nameof(SourceLanguage), typeof(LearningLanguage), typeof(LanguageBadge), new PropertyMetadata(LearningLanguage.Japanese));
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LanguageBadge), new PropertyMetadata(false));

        /// <summary>由XAML创建纯视觉组件。</summary>
        public LanguageBadge() => InitializeComponent();
        public LearningLanguage SourceLanguage { get => (LearningLanguage)GetValue(SourceLanguageProperty); set => SetValue(SourceLanguageProperty, value); }
        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    }
}
