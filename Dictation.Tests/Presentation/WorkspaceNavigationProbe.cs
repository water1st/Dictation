using Dictation.Core.Configuration;
using Dictation.Presentation.Navigation;

namespace Dictation.Tests.Presentation
{
    internal sealed class WorkspaceNavigationProbe : IWorkspaceNavigation
    {
        internal List<LearningLanguage> Languages { get; } = new();
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? Completion { get; init; }

        public async Task OpenLanguageWorkspaceAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            Languages.Add(language);
            Entered.TrySetResult();
            if (Completion is not null) await Completion.Task.WaitAsync(cancellationToken);
        }
    }
}
