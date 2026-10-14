namespace Dictation.Core.Kana
{
    /// <summary>向页面和每日统计提供独立五十音计划读取能力。</summary>
    public interface IKanaPlanReader
    {
        /// <summary>读取保存计划；尚未保存时返回默认禁用计划。</summary>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>已校验计划。</returns>
        Task<KanaPlanDto> LoadAsync(CancellationToken cancellationToken = default);
    }
}
