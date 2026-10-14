using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Practice;

namespace Dictation.Presentation.Practice
{
    /// <summary>适配共用练习页面实际使用的操作；模式各自调用原有 Core 能力，成绩不互通。</summary>
    internal interface IPracticeSessionWorkflow
    {
        /// <summary>用于页面标题的学习或复习名称。</summary>
        string ModeLabel { get; }

        /// <summary>启动或恢复固定语言的会话；复习无到期候选时返回空，不伪造完成。</summary>
        /// <param name="language">首页选定的语言。</param>
        /// <param name="selectedWordGroupIds">学习手选范围；复习范围由复习计划管理。</param>
        /// <param name="cancellationToken">取消准备的令牌。</param>
        Task<PracticeSessionSnapshot?> StartOrResumeAsync(LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds, CancellationToken cancellationToken);

        /// <summary>读取本轮浏览成员，不推进作答状态。</summary>
        /// <param name="sessionId">原业务会话标识。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        Task<IReadOnlyList<ImportWordGroupMemberDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken);

        /// <summary>读取原业务持久化的待答题；完成时返回空。</summary>
        /// <param name="sessionId">原业务会话标识。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken);

        /// <summary>提交到当前模式的业务能力，并转换为共用页面反馈，不在表现层判题。</summary>
        /// <param name="sessionId">原业务会话标识。</param>
        /// <param name="questionId">当前题目标识。</param>
        /// <param name="submittedText">用户实际输入的答案。</param>
        /// <param name="cancellationToken">取消提交的令牌。</param>
        Task<PracticeAnswerFeedback> SubmitAnswerAsync(string sessionId, long questionId, string? submittedText, CancellationToken cancellationToken);
    }
}
