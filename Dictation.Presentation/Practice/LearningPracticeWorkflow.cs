using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;

namespace Dictation.Presentation.Practice
{
    /// <summary>把学习会话的既有业务能力适配为共用练习页面，不处理判题或数据库。</summary>
    internal sealed class LearningPracticeWorkflow : IPracticeSessionWorkflow
    {
        private readonly ILearningSessionOperations operations;
        private readonly ILearningPlanReader planReader;

        /// <summary>组合学习用例与计划读取能力。</summary>
        /// <param name="operations">原有学习启动、浏览、读取和答题能力。</param>
        /// <param name="planReader">学习计划读取能力。</param>
        public LearningPracticeWorkflow(ILearningSessionOperations operations, ILearningPlanReader planReader)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.planReader = planReader ?? throw new ArgumentNullException(nameof(planReader));
        }
        /// <inheritdoc />
        public string ModeLabel => "学习";
        /// <inheritdoc />
        public async Task<PracticeSessionSnapshot?> StartOrResumeAsync(LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await planReader.LoadAsync(language, cancellationToken);
            var session = await operations.StartOrResumeAsync(new StartLearningSessionRequestDto(language, selectedWordGroupIds), cancellationToken);
            return PracticeSessionMapper.ToSnapshot(session, plan);
        }
        /// <inheritdoc />
        public async Task<IReadOnlyList<ImportWordGroupMemberDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken) =>
            PracticeSessionMapper.ToMembers(await operations.GetBrowseItemsAsync(sessionId, cancellationToken));
        /// <inheritdoc />
        public Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken) =>
            operations.GetCurrentQuestionAsync(sessionId, cancellationToken);
        /// <inheritdoc />
        public async Task<PracticeAnswerFeedback> SubmitAnswerAsync(string sessionId, long questionId, string? submittedText, CancellationToken cancellationToken)
        {
            var result = await operations.SubmitAnswerAsync(new SubmitLearningAnswerRequestDto(sessionId, questionId, submittedText), cancellationToken);
            // 学习的原始总结未提供整组数，只在结算时读取真实范围，不维护第二份可变成绩。
            var groupCount = result.Summary is null ? 0 : (await operations.GetBrowseItemsAsync(sessionId, cancellationToken)).Count;
            return PracticeSessionMapper.ToFeedback(result, groupCount);
        }
    }
}
