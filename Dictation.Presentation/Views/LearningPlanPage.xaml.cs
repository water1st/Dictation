using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Views
{
    /// <summary>
    /// 显示学习计划选项、学习库与启动选择。
    /// </summary>
    public sealed partial class LearningPlanPage : System.Windows.Controls.UserControl
    {
        private bool restoringScroll;
        /// <summary>
        /// 显示学习计划选项、学习库与启动选择。
        /// </summary>
        public LearningPlanPage()
        {
            InitializeComponent();
            Loaded += OnPlanLoaded;
            Unloaded += OnPlanUnloaded;
        }

        private async void OnPlanLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LearningPlanViewModel viewModel) return;
            restoringScroll = true;
            var offset = viewModel.ScrollOffset;
            await Dispatcher.InvokeAsync(() =>
            {
                if (IsLoaded && ReferenceEquals(DataContext, viewModel))
                {
                    PlanScrollViewer.ScrollToVerticalOffset(offset);
                    PlanScrollViewer.UpdateLayout();
                }
                restoringScroll = false;
            }, DispatcherPriority.Loaded);
        }

        private void OnPlanUnloaded(object sender, RoutedEventArgs e)
        {
            restoringScroll = true;
        }

        private void OnPlanContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = PlanContent.ActualWidth;
            var horizontal = width >= 760;
            var cardWidth = horizontal ? (width - 12) / 2 : width;
            RhythmCard.Width = Math.Max(0, cardWidth);
            PracticeCard.Width = Math.Max(0, cardWidth);
            RhythmCard.Margin = horizontal ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 12);
        }

        private async void OnPlanScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!IsLoaded || restoringScroll || !ReferenceEquals(e.OriginalSource, PlanScrollViewer)
                || DataContext is not LearningPlanViewModel viewModel) return;
            viewModel.ScrollOffset = PlanScrollViewer.VerticalOffset;
            if (e.VerticalChange <= 0 || e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0
                || PlanScrollViewer.ScrollableHeight - PlanScrollViewer.VerticalOffset > 80
                || viewModel.IsBusy)
            {
                return;
            }

            try
            {
                var offset = PlanScrollViewer.VerticalOffset;
                await viewModel.LoadMoreAsync();
                PlanScrollViewer.ScrollToVerticalOffset(offset);
            }
            catch (OperationCanceledException)
            {
                // 页面寿命结束时允许取消本次按需加载。
            }
        }
    }
}
