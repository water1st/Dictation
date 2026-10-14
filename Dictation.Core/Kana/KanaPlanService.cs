using Dictation.DataAccess.Kana;

namespace Dictation.Core.Kana
{
    /// <summary>编排五十音范围校验和计划读写。</summary>
    internal sealed class KanaPlanService : IKanaPlanReader, IKanaPlanEditor
    {
        private readonly KanaPlanDataAccessor data;
        /// <summary>组合计划列读写，不在DataAccess生成默认业务值。</summary>
        /// <param name="data">计划CRUD能力。</param>
        public KanaPlanService(KanaPlanDataAccessor data) => this.data = data ?? throw new ArgumentNullException(nameof(data));
        /// <inheritdoc />
        public async Task<KanaPlanDto> LoadAsync(CancellationToken cancellationToken = default)
        {
            var stored = await data.LoadAsync(cancellationToken);
            var plan = stored is null ? new KanaPlanDto(false, true, false, true, false, false, false, 20) : KanaPlanMapper.ToDto(stored);
            Validate(plan);
            return plan;
        }
        /// <inheritdoc />
        public Task SaveAsync(KanaPlanDto plan, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Validate(plan);
            return data.SaveAsync(KanaPlanMapper.ToPo(plan), cancellationToken);
        }

        /// <summary>保证两组独立范围各保留一项，不因禁用每日计划而保存非法配置。</summary>
        /// <param name="plan">完整业务计划。</param>
        private static void Validate(KanaPlanDto plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (!plan.Hiragana && !plan.Katakana) throw new ArgumentException("平假名与片假名至少选择一项。", nameof(plan));
            if (!plan.Clear && !plan.Voiced && !plan.Contracted) throw new ArgumentException("清音、浊音与半浊音、拗音至少选择一项。", nameof(plan));
            if (plan.DailyTarget is < 1 or > 214) throw new ArgumentException("每日数量必须为1–214。", nameof(plan));
        }
    }
}
