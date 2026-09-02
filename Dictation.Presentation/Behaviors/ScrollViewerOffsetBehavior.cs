using System.Windows;
using System.Windows.Controls;

namespace Dictation.Presentation.Behaviors
{
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

        public static double GetVerticalOffset(DependencyObject dependencyObject)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            return (double)dependencyObject.GetValue(VerticalOffsetProperty);
        }

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

        public static bool GetIsAttached(DependencyObject dependencyObject)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            return (bool)dependencyObject.GetValue(IsAttachedProperty);
        }

        public static void SetIsAttached(DependencyObject dependencyObject, bool value)
        {
            ArgumentNullException.ThrowIfNull(dependencyObject);
            dependencyObject.SetValue(IsAttachedProperty, value);
        }
    }
}
