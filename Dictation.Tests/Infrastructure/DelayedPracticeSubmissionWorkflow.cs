using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Practice;
using Dictation.Presentation.Practice;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>在真实提交完成后延迟交还结果，复现导航与反馈发布之间的竞争。</summary>
    internal sealed class DelayedPracticeSubmissionWorkflow : IPracticeSessionWorkflow
    {
        private readonly IPracticeSessionWorkflow inner;

        internal DelayedPracticeSubmissionWorkflow(IPracticeSessionWorkflow inner)
        {
            this.inner = inner;
        }

        internal TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ModeLabel => inner.ModeLabel;

        public Task<PracticeSessionSnapshot?> StartOrResumeAsync(LearningLanguage language, IReadOnlyList<string>? selectedWordGroupIds, CancellationToken cancellationToken)
        {
            return inner.StartOrResumeAsync(language, selectedWordGroupIds, cancellationToken);
        }

        public Task<IReadOnlyList<ImportWordGroupMemberDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken)
        {
            return inner.GetBrowseItemsAsync(sessionId, cancellationToken);
        }

        public Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken)
        {
            return inner.GetCurrentQuestionAsync(sessionId, cancellationToken);
        }

        public async Task<PracticeAnswerFeedback> SubmitAnswerAsync(string sessionId, long questionId, string? submittedText, CancellationToken cancellationToken)
        {
            var result = await inner.SubmitAnswerAsync(sessionId, questionId, submittedText, cancellationToken);
            Committed.TrySetResult();
            await Release.Task;
            return result;
        }
    }
}
