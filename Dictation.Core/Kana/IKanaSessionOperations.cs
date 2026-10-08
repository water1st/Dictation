namespace Dictation.Core.Kana
{
    /// <summary>提供五十音启动与客观作答用例，写入都经过Core规则。</summary>
    public interface IKanaSessionOperations
    {
        /// <summary>优先恢复旧轮次，否则按启用计划创建当日轮次。</summary>
        /// <param name="cancellationToken">取消操作令牌。</param>
        /// <returns>轮次；禁用、目标已完成或无到期项目时为空。</returns>
        Task<KanaSessionDto?> StartOrResumeAsync(CancellationToken cancellationToken = default);
        /// <summary>检查题序并原子保存观察、状态和必要的独立FSRS。</summary>
        /// <param name="sessionId">正在作答轮次。</param>
        /// <param name="questionOrdinal">页面当前题序，过期提交拒绝。</param>
        /// <param name="submittedText">用户实际答题文本，空白判错。</param>
        /// <param name="cancellationToken">取消保存令牌。</param>
        /// <returns>即时正确答案、下一题和本轮累计错误。</returns>
        Task<KanaFeedbackDto> SubmitAsync(string sessionId, int questionOrdinal, string? submittedText, CancellationToken cancellationToken = default);
    }
}
