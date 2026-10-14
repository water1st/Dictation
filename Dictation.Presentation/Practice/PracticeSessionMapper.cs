using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;

namespace Dictation.Presentation.Practice
{
    /// <summary>纯转换两种业务合同为页面绑定模型，不查询数据、不判题、不保存成绩。</summary>
    internal static class PracticeSessionMapper
    {
        /// <summary>保留学习会话状态、范围和浏览词形。</summary>
        /// <param name="session">原学习会话。</param>
        /// <param name="plan">当前学习浏览计划。</param>
        internal static PracticeSessionSnapshot ToSnapshot(LearningSessionDto session, LearningPlanDto plan) =>
            new(session.SessionId, session.Language, session.Status == LearningSessionStatus.Completed, session.WordGroupIds, plan.FormCodes);

        /// <summary>复习浏览沿用启动快照，不读取后来修改的设置。</summary>
        /// <param name="session">活动复习会话及其原计划快照。</param>
        internal static PracticeSessionSnapshot ToSnapshot(ReviewSessionDto session) =>
            new(session.SessionId, session.Language, false, session.WordGroupIds, session.PlanSnapshot.FormCodes);

        /// <summary>按原学习整组和成员顺序展开浏览列表。</summary>
        /// <param name="groups">业务返回的本轮学习词组。</param>
        internal static IReadOnlyList<ImportWordGroupMemberDto> ToMembers(IReadOnlyList<LearningBrowseItemDto> groups) =>
            groups.SelectMany(group => group.Members).ToArray();

        /// <summary>按原复习整组和成员顺序展开浏览列表。</summary>
        /// <param name="groups">业务返回的本轮复习词组。</param>
        internal static IReadOnlyList<ImportWordGroupMemberDto> ToMembers(IReadOnlyList<ImportWordGroupDto> groups) =>
            groups.SelectMany(group => group.Members).ToArray();

        /// <summary>保留学习反馈和原始错误；学习合同没有成员标识，不伪造标识。</summary>
        /// <param name="result">原学习答题结果。</param>
        /// <param name="completedGroupCount">结算时真实本轮整组数量。</param>
        internal static PracticeAnswerFeedback ToFeedback(LearningAnswerResultDto result, int completedGroupCount) =>
            new(result.WasCorrect, result.CorrectAnswer, result.HadErrorInSession, result.NextQuestion,
                result.Summary is null ? null : new PracticeSessionSummary(result.Summary.SessionId, result.Summary.AnsweredCount,
                    result.Summary.Errors.Select(error => new PracticeErrorSummary(error.WordGroupId, null, error.FormCode, error.CorrectAnswer)).ToArray(),
                    completedGroupCount), result.SpokenText);

        /// <summary>保留复习独立成员错误、完成整组数及正确答案读音。</summary>
        /// <param name="result">原复习答题结果。</param>
        internal static PracticeAnswerFeedback ToFeedback(ReviewAnswerResultDto result) =>
            new(result.WasCorrect, result.ExpectedAnswer, result.HadErrorInSession, result.NextQuestion,
                result.Summary is null ? null : new PracticeSessionSummary(result.Summary.SessionId, result.Summary.AnsweredCount,
                    result.Summary.Errors.Select(error => new PracticeErrorSummary(error.WordGroupId, error.MemberId, error.FormCode, error.ExpectedAnswer)).ToArray(),
                    result.Summary.CompletedWordGroupCount), result.ExpectedSpokenText);
    }
}
