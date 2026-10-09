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
    }
}
