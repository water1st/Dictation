using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Mistakes;
using Dictation.Core.Practice;

namespace Dictation.Presentation.Practice
{
    /// <summary>仅替换错题启动选词，其余行为组合原复习适配，不复制业务或页面。</summary>
    internal sealed class WrongAnswerPracticeWorkflow : IPracticeSessionWorkflow
    {
        private readonly IWrongAnswerPractice starter;
        private readonly ReviewPracticeWorkflow review;

        /// <summary>组合错题启动与既有复习查询、浏览和作答能力。</summary>
        /// <param name="starter">准备错误形式的原复习会话。</param>
        /// <param name="review">原复习页面适配，提供启动以外的行为。</param>
        public WrongAnswerPracticeWorkflow(IWrongAnswerPractice starter, ReviewPracticeWorkflow review)
        {
            this.starter = starter ?? throw new ArgumentNullException(nameof(starter));
            this.review = review ?? throw new ArgumentNullException(nameof(review));
        }

        /// <inheritdoc />
        public string ModeLabel => "错题重练";
        /// <inheritdoc />
        public async Task<PracticeSessionSnapshot?> StartOrResumeAsync(LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds, CancellationToken cancellationToken) =>
            PracticeSessionMapper.ToSnapshot(await starter.StartOrResumeAsync(language,
                selectedWordGroupIds ?? Array.Empty<string>(), cancellationToken));
        /// <inheritdoc />
        public Task<IReadOnlyList<ImportWordGroupMemberDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken) =>
            review.GetBrowseItemsAsync(sessionId, cancellationToken);
        /// <inheritdoc />
        public Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken) =>
            review.GetCurrentQuestionAsync(sessionId, cancellationToken);
        /// <inheritdoc />
        public Task<PracticeAnswerFeedback> SubmitAnswerAsync(string sessionId, long questionId, string? submittedText, CancellationToken cancellationToken) =>
            review.SubmitAnswerAsync(sessionId, questionId, submittedText, cancellationToken);
    }
}
