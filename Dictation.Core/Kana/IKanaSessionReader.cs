namespace Dictation.Core.Kana
{
    /// <summary>读取当前听写题，不创建作答观察或承担首页统计。</summary>
    public interface IKanaSessionReader
    {
        /// <summary>读取原轮次当前题；结束轮次返回空，不存在的轮次报错。</summary>
        /// <param name="sessionId">已存在轮次标识。</param>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>不供UI展示答案的题面与语音文本。</returns>
        Task<KanaQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default);
        /// <summary>回读已经提交的题序反馈，不提交答案，不重复记忆更新。</summary>
        /// <param name="sessionId">原轮次标识，包含已经完成的轮次。</param>
        /// <param name="questionOrdinal">需要确认的原提交题序。</param>
        /// <param name="cancellationToken">取消只读恢复。</param>
        /// <returns>已保存的反馈；该题尚未提交时为空。</returns>
        Task<KanaFeedbackDto?> GetSubmissionFeedbackAsync(string sessionId, int questionOrdinal, CancellationToken cancellationToken = default);
    }
}
