namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 携带一次转场的前后页面、标题与动画方向。
    /// </summary>
    public sealed class NavigationTransitionEventArgs : EventArgs
    {
        /// <summary>
        /// 携带一次转场的前后页面、标题与动画方向。
        /// </summary>
        /// <param name="previousPage">离开页面的视图或模型。</param>
        /// <param name="previousTitle">离开页面的标题。</param>
        /// <param name="currentPage">进入页面的视图或模型。</param>
        /// <param name="currentTitle">进入页面的标题。</param>
        /// <param name="direction">决定动画方向的导航类型。</param>
        public NavigationTransitionEventArgs(
            object previousPage,
            string previousTitle,
            object currentPage,
            string currentTitle,
            NavigationDirection direction)
        {
            PreviousPage = previousPage ?? throw new ArgumentNullException(nameof(previousPage));
            PreviousTitle = previousTitle ?? throw new ArgumentNullException(nameof(previousTitle));
            CurrentPage = currentPage ?? throw new ArgumentNullException(nameof(currentPage));
            CurrentTitle = currentTitle ?? throw new ArgumentNullException(nameof(currentTitle));
            Direction = direction;
        }

        public object PreviousPage { get; }

        public string PreviousTitle { get; }

        public object CurrentPage { get; }

        public string CurrentTitle { get; }

        public NavigationDirection Direction { get; }
    }
}
