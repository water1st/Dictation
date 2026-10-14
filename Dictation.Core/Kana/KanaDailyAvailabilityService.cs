using Dictation.Core.Review;
using Dictation.DataAccess.Kana;
using System.Globalization;

namespace Dictation.Core.Kana
{
    /// <summary>提供五十音共用适用范围与只读每日进度，不依赖会话业务服务。</summary>
    internal sealed class KanaDailyAvailabilityService
    {
        private readonly IKanaPlanReader plans;
        private readonly KanaSessionDataAccessor data;
        private readonly TimeProvider clock;

        /// <summary>组合计划、原记忆与完成事实，避免统计服务和会话服务循环依赖。</summary>
        /// <param name="plans">已校验的五十音计划。</param>
        /// <param name="data">独立五十音数据访问。</param>
        /// <param name="clock">当前日期与到期时钟。</param>
        public KanaDailyAvailabilityService(IKanaPlanReader plans, KanaSessionDataAccessor data, TimeProvider clock)
        {
            this.plans = plans; this.data = data; this.clock = clock;
        }

        /// <summary>按原范围及模式计算当日完成、可练和有效目标。</summary>
        /// <param name="token">取消读取。</param>
        internal async Task<KanaProgressDto> GetProgressAsync(CancellationToken token)
        {
            var plan = await plans.LoadAsync(token);
            var active = await data.GetActiveAsync(token);
            var symbols = KanaCatalog.Select(plan);
            var completed = (await data.GetCompletedIdsAsync(clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), token)).ToHashSet();
            var memories = (await data.GetMemoriesAsync(token)).ToDictionary(item => item.KanaId, KanaSessionMapper.ToState);
            var done = symbols.Count(symbol => completed.Contains(symbol.Id));
            var available = SelectEligible(symbols, completed, memories, plan.ReviewAll).Count;
            var target = plan.ReviewAll ? symbols.Count : Math.Min(plan.DailyTarget, done + available);
            return new(plan.Enabled, done, plan.Enabled ? target : 0, available, active is not null);
        }

        /// <summary>全量忽略到期限制，限量只安排未测或到期且今天尚未完成的项目。</summary>
        /// <param name="symbols">启用范围。</param>
        /// <param name="completed">已完成项目 ID。</param>
        /// <param name="memories">原记忆状态。</param>
        /// <param name="all">是否全量。</param>
        internal IReadOnlyList<KanaSymbol> SelectEligible(IReadOnlyList<KanaSymbol> symbols, HashSet<string> completed,
            IReadOnlyDictionary<string, FsrsMemoryState?> memories, bool all)
        {
            var now = clock.GetUtcNow();
            return symbols.Where(symbol => !completed.Contains(symbol.Id))
                .Where(symbol => all || !memories.TryGetValue(symbol.Id, out var state) || state is null || state.DueAtUtc <= now).ToArray();
        }
    }
}
