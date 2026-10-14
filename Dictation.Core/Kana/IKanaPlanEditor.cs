namespace Dictation.Core.Kana
{
    /// <summary>只暴露五十音计划保存能力，不让页面依赖会话写入。</summary>
    public interface IKanaPlanEditor
    {
        /// <summary>校验并保存后续轮次计划，不修改原活动轮次。</summary>
        /// <param name="plan">至少一种字形、一种音类及有效数量的计划。</param>
        /// <param name="cancellationToken">取消保存令牌。</param>
        Task SaveAsync(KanaPlanDto plan, CancellationToken cancellationToken = default);
    }
}
