using Dictation.DataAccess.Kana;

namespace Dictation.Core.Kana
{
    /// <summary>只转换五十音计划的DTO和列对象，不校验、不查询或写入。</summary>
    internal static class KanaPlanMapper
    {
        /// <summary>读取列值为业务计划。</summary>
        /// <param name="po">已保存的计划列。</param>
        /// <returns>相同选项的业务DTO。</returns>
        internal static KanaPlanDto ToDto(KanaPlanPo po) => new(po.Enabled, po.Hiragana, po.Katakana, po.Clear,
            po.Voiced, po.Contracted, po.ReviewAll, po.DailyTarget);
        /// <summary>转换已校验业务选项为持久化列。</summary>
        /// <param name="dto">完整业务计划。</param>
        /// <returns>不携带UI状态的列对象。</returns>
        internal static KanaPlanPo ToPo(KanaPlanDto dto) => new()
        {
            Enabled = dto.Enabled, Hiragana = dto.Hiragana, Katakana = dto.Katakana, Clear = dto.Clear,
            Voiced = dto.Voiced, Contracted = dto.Contracted, ReviewAll = dto.ReviewAll, DailyTarget = dto.DailyTarget
        };
    }
}
