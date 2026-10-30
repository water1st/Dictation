using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Dictation.Presentation.Controls
{
    /// <summary>在页面内容区右缘呈现并适配页面主滚动条。</summary>
    public sealed class PageScrollHost : ContentControl
    {
        public static readonly DependencyProperty IsPageScrollViewerProperty = DependencyProperty.RegisterAttached(
            "IsPageScrollViewer", typeof(bool), typeof(PageScrollHost), new PropertyMetadata(false));

        private ScrollBar? pageScrollBar;
        private ScrollViewer? targetViewer;
        private ScrollBarVisibility originalVisibility;
        private bool isSynchronizing;

        /// <summary>跟随页面模板和装载周期连接主滚动容器。</summary>
        public PageScrollHost()
        {
            LayoutUpdated += OnLayoutUpdated;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            DetachViewer();
            if (pageScrollBar is not null)
            {
                pageScrollBar.ValueChanged -= OnBarValueChanged;
            }
            base.OnApplyTemplate();
            pageScrollBar = GetTemplateChild("PART_PageScrollBar") as ScrollBar;
            if (pageScrollBar is not null)
            {
                pageScrollBar.ValueChanged += OnBarValueChanged;
            }
            (GetTemplateChild("PART_PageContent") as ContentPresenter)?.ApplyTemplate();
            ConnectViewer();
        }

        /// <inheritdoc />
        protected override void OnContentChanged(object oldContent, object newContent)
        {
            DetachViewer();
            base.OnContentChanged(oldContent, newContent);
        }

        /// <summary>读取滚动容器是否承担页面主滚动。</summary>
        public static bool GetIsPageScrollViewer(DependencyObject element)
        {
            return (bool)element.GetValue(IsPageScrollViewerProperty);
        }

        /// <summary>标记页面主滚动容器；列表和输入框不使用此标记。</summary>
        public static void SetIsPageScrollViewer(DependencyObject element, bool value)
        {
            element.SetValue(IsPageScrollViewerProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ConnectViewer();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachViewer();
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (targetViewer is not null && !IsAncestorOf(targetViewer))
            {
                DetachViewer();
            }
            if (targetViewer is null)
            {
                ConnectViewer();
            }
        }

        private void ConnectViewer()
        {
            if (pageScrollBar is null || targetViewer is not null)
            {
                return;
            }
            targetViewer = FindViewer(this);
            if (targetViewer is null)
            {
                pageScrollBar.Visibility = Visibility.Collapsed;
                return;
            }
            originalVisibility = targetViewer.VerticalScrollBarVisibility;
            targetViewer.SetCurrentValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            targetViewer.ScrollChanged += OnViewerScrollChanged;
            targetViewer.IsEnabledChanged += OnViewerIsEnabledChanged;
            SynchronizeBar();
        }

        private void DetachViewer()
        {
            if (targetViewer is not null)
            {
                var previous = targetViewer;
                targetViewer = null;
                previous.ScrollChanged -= OnViewerScrollChanged;
                previous.IsEnabledChanged -= OnViewerIsEnabledChanged;
                previous.SetCurrentValue(ScrollViewer.VerticalScrollBarVisibilityProperty, originalVisibility);
            }
            if (pageScrollBar is not null)
            {
                pageScrollBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OnViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, targetViewer))
            {
                SynchronizeBar();
            }
        }

        private void OnViewerIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SynchronizeBar();
        }

        private void SynchronizeBar()
        {
            if (pageScrollBar is null || targetViewer is null)
            {
                return;
            }
            isSynchronizing = true;
            try
            {
                pageScrollBar.Maximum = Math.Max(0, targetViewer.ScrollableHeight);
                pageScrollBar.ViewportSize = targetViewer.ViewportHeight;
                pageScrollBar.LargeChange = targetViewer.ViewportHeight;
                pageScrollBar.SmallChange = 16;
                pageScrollBar.Value = targetViewer.VerticalOffset;
                pageScrollBar.IsEnabled = targetViewer.IsEnabled;
                pageScrollBar.Visibility = targetViewer.ScrollableHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                isSynchronizing = false;
            }
        }

        private void OnBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isSynchronizing)
            {
                targetViewer?.ScrollToVerticalOffset(e.NewValue);
            }
        }

        private static ScrollViewer? FindViewer(DependencyObject root)
        {
            if (root is ScrollViewer viewer && GetIsPageScrollViewer(viewer))
            {
                return viewer;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var result = FindViewer(VisualTreeHelper.GetChild(root, index));
                if (result is not null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
