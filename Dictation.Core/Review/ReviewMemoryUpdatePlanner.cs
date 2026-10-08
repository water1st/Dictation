using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>复用 FSRS 内核评价本轮完成形式，既有记忆续算，本轮任意错误保持 Again。</summary>
    internal sealed class ReviewMemoryUpdatePlanner
    {
        private readonly FsrsScheduler scheduler;

        /// <summary>组合已有纯 FSRS 算法，不建立第二套记忆公式。</summary>
        /// <param name="scheduler">计算 Good／Again 的记忆内核。</param>
        public ReviewMemoryUpdatePlanner(FsrsScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>为这次首次完成的形式生成一次更新，未完成形式不评价。</summary>
        /// <param name="completed">本次转为完成的独立词形。</param>
        /// <param name="previous">持久化的既有记忆，不含本次尚未提交的变化。</param>
        /// <param name="now">本次客观评价时间。</param>
        internal IReadOnlyList<ReviewFormMemoryPo> CreateUpdates(IReadOnlyList<PracticeFormUnitPo> completed,
            IReadOnlyList<ReviewStoredMemoryPo> previous, DateTimeOffset now)
        {
            var byForm = previous.ToDictionary(value => (value.MemberId, value.FormCode));
            return completed.Select(unit =>
            {
                var state = byForm.TryGetValue((unit.MemberId, unit.FormCode), out var stored) ? ReviewLibraryMapper.ToState(stored) : null;
                var next = scheduler.Schedule(state, unit.ErrorCount == 0, now);
                return ReviewMemoryMapper.ToPo(unit.WordGroupId, unit.MemberId, unit.FormCode, next);
            }).ToArray();
        }
    }
}
