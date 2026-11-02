using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Dictation.Presentation.Behaviors
{
    /// <summary>在空候选时复位展开请求，并保留原有双向绑定与可编辑输入。</summary>
    public static class EmptyComboBoxBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(EmptyComboBoxBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyPropertyDescriptor openDescriptor = DependencyPropertyDescriptor.FromProperty(
            ComboBox.IsDropDownOpenProperty, typeof(ComboBox));
        private static readonly DependencyPropertyDescriptor itemsDescriptor = DependencyPropertyDescriptor.FromProperty(
            ComboBox.HasItemsProperty, typeof(ComboBox));

        public static bool GetIsEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
            if (element is not ComboBox combo) return;
            combo.Loaded -= OnLoaded;
            combo.Unloaded -= OnUnloaded;
            Detach(combo);
            if (args.NewValue is true)
            {
                combo.Loaded += OnLoaded;
                combo.Unloaded += OnUnloaded;
                if (combo.IsLoaded) Attach(combo);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs args)
        {
            Attach((ComboBox)sender);
        }

        private static void OnUnloaded(object sender, RoutedEventArgs args)
        {
            Detach((ComboBox)sender);
        }

        private static void Attach(ComboBox combo)
        {
            Detach(combo);
            openDescriptor.AddValueChanged(combo, OnStateChanged);
            itemsDescriptor.AddValueChanged(combo, OnStateChanged);
            OnStateChanged(combo, EventArgs.Empty);
        }

        private static void Detach(ComboBox combo)
        {
            openDescriptor.RemoveValueChanged(combo, OnStateChanged);
            itemsDescriptor.RemoveValueChanged(combo, OnStateChanged);
        }

        private static void OnStateChanged(object? sender, EventArgs args)
        {
            if (sender is ComboBox { HasItems: false, IsDropDownOpen: true } combo)
            {
                // 立即撤销这次空展开；候选在同轮到达时也不会沿用该请求。
                combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
                // 源到目标传递期间的同步回写可能被忽略，结束后显式同步当前值。
                combo.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
                {
                    if (GetIsEnabled(combo) && combo.IsLoaded && !combo.IsDropDownOpen)
                    {
                        combo.GetBindingExpression(ComboBox.IsDropDownOpenProperty)?.UpdateSource();
                    }
                }));
            }
        }
    }
}
