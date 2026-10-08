using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Practice;
using Dictation.Core.Review;

namespace Dictation.Presentation.Practice
{
    /// <summary>把独立复习用例适配为共用练习页面，绝不转换为学习请求或写学习成绩。</summary>
    internal sealed class ReviewPracticeWorkflow : IPracticeSessionWorkflow
    {
        private readonly IReviewSessionStarter starter;
        private readonly IReviewSessionReader reader;
        private readonly IReviewQuestionReader questions;
        private readonly IReviewSessionAnswerer answerer;

        /// <summary>组合复习页面需要的四个小能力。</summary>
        /// <param name="starter">启动或恢复复习会话。</param>
        /// <param name="reader">读取本轮完整词组。</param>
        /// <param name="questions">读取持久化待答题。</param>
        /// <param name="answerer">提交复习客观成绩与记忆。</param>
        public ReviewPracticeWorkflow(IReviewSessionStarter starter, IReviewSessionReader reader, IReviewQuestionReader questions, IReviewSessionAnswerer answerer)
        {
            this.starter = starter ?? throw new ArgumentNullException(nameof(starter));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.questions = questions ?? throw new ArgumentNullException(nameof(questions));
            this.answerer = answerer ?? throw new ArgumentNullException(nameof(answerer));
        }
        /// <inheritdoc />
        public string ModeLabel => "复习";
        /// <inheritdoc />
        public async Task<PracticeSessionSnapshot?> StartOrResumeAsync(LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedWordGroupIds is { Count: > 0 })
                throw new ArgumentException("复习选词由复习计划管理，不能使用学习手选范围。", nameof(selectedWordGroupIds));
            var session = await starter.StartOrResumeAsync(language, cancellationToken);
            return session is null ? null : PracticeSessionMapper.ToSnapshot(session);
        }
        /// <inheritdoc />
        public async Task<IReadOnlyList<ImportWordGroupMemberDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken) =>
            PracticeSessionMapper.ToMembers(await reader.GetBrowseItemsAsync(sessionId, cancellationToken));
        /// <inheritdoc />
        public Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken) =>
            questions.GetCurrentQuestionAsync(sessionId, cancellationToken);
        /// <inheritdoc />
        public async Task<PracticeAnswerFeedback> SubmitAnswerAsync(string sessionId, long questionId, string? submittedText, CancellationToken cancellationToken) =>
            PracticeSessionMapper.ToFeedback(await answerer.SubmitAnswerAsync(new SubmitReviewAnswerRequestDto(sessionId, questionId, submittedText), cancellationToken));
    }
}
