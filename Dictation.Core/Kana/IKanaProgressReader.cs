namespace Dictation.Core.Kana
{
    /// <summary>提供独立五十音每日进度，不启动轮次或读取待答题。</summary>
    public interface IKanaProgressReader
    {
        /// <summary>读取今天适用目标、实际完成数量和活动轮次状态。</summary>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>独立五十音进度，不包含词汇数量。</returns>
        Task<KanaProgressDto> GetProgressAsync(CancellationToken cancellationToken = default);
    }
}
