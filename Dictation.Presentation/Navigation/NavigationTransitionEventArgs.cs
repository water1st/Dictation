namespace Dictation.Presentation.Navigation
{
    public sealed class NavigationTransitionEventArgs : EventArgs
    {
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
