using System.Windows;
using System.Windows.Controls;

namespace Dictation.Presentation.Behaviors
{
    /// <summary>
    /// 把滚动容器偏移与页面状态进行双向同步。
    /// </summary>
    public static class ScrollViewerOffsetBehavior
    {
        private static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsAttached",
                typeof(bool),
                typeof(ScrollViewerOffsetBehavior),
                new PropertyMetadata(false, OnIsAttachedChanged));

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(ScrollViewerOffsetBehavior),
                new PropertyMetadata(false));

        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "VerticalOffset",
                typeof(double),
                typeof(ScrollViewerOffsetBehavior),
                new FrameworkPropertyMetadata(
                    0d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnVerticalOffsetChanged));

        /// <summary>
        /// 读取附加属性保存的垂直滚动偏移。
        /// </summary>
        /// <param name="dependencyObject">持有滚动附加属性的 WPF 对象。</param>
        /// <returns>保存的垂直偏移。</returns>
        public static double GetVerticalOffset(DependencyObject dependencyObject)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            return (double)dependencyObject.GetValue(VerticalOffsetProperty);
        }

        /// <summary>
        /// 设置附加属性并同步到滚动容器。
        /// </summary>
        /// <param name="dependencyObject">持有滚动附加属性的 WPF 对象。</param>
        /// <param name="value">从顶部起算的垂直偏移。</param>
        public static void SetVerticalOffset(DependencyObject dependencyObject, double value)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            dependencyObject.SetValue(VerticalOffsetProperty, value);
        }

        private static void OnVerticalOffsetChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not ScrollViewer viewer
                || (bool)viewer.GetValue(IsUpdatingProperty))
            {
                return;
            }

            if (!GetIsAttached(viewer))
            {
                SetIsAttached(viewer, true);
            }

            viewer.ScrollToVerticalOffset((double)e.NewValue);
        }

        private static void OnIsAttachedChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not ScrollViewer viewer)
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                viewer.ScrollChanged += OnScrollChanged;
                viewer.ScrollToVerticalOffset(GetVerticalOffset(viewer));
            }
            else
            {
                viewer.ScrollChanged -= OnScrollChanged;
            }
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer viewer
                || (bool)viewer.GetValue(IsUpdatingProperty))
            {
                return;
            }

            viewer.SetValue(IsUpdatingProperty, true);
            try
            {
                viewer.SetCurrentValue(VerticalOffsetProperty, viewer.VerticalOffset);
            }
            finally
            {
                viewer.SetValue(IsUpdatingProperty, false);
            }
        }

        /// <summary>
        /// 读取滚动事件同步行为是否启用。
        /// </summary>
        /// <param name="dependencyObject">持有滚动附加属性的 WPF 对象。</param>
        /// <returns>是否已启用滚动同步。</returns>
        public static bool GetIsAttached(DependencyObject dependencyObject)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            return (bool)dependencyObject.GetValue(IsAttachedProperty);
        }

        /// <summary>
        /// 启用或解除滚动事件与附加属性之间的同步。
        /// </summary>
        /// <param name="dependencyObject">持有滚动附加属性的 WPF 对象。</param>
        /// <param name="value">是否启用滚动同步行为。</param>
        public static void SetIsAttached(DependencyObject dependencyObject, bool value)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            dependencyObject.SetValue(IsAttachedProperty, value);
        }
    }
}
