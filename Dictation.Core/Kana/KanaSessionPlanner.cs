using Dictation.DataAccess.Kana;

namespace Dictation.Core.Kana
{
    /// <summary>只决定五十音当前状态和重试队列，不做CRUD或FSRS计算。</summary>
    internal sealed class KanaSessionPlanner
    {
        private readonly Random random;
        /// <summary>复用唯一容器的随机源，支持可重复边界测试。</summary>
        /// <param name="random">用于重试间隔与首次顺序的随机源。</param>
        public KanaSessionPlanner(Random random) => this.random = random ?? throw new ArgumentNullException(nameof(random));

        /// <summary>首次正确即完成；有过错误的项目连续两次正确才完成。</summary>
        /// <param name="item">当前独立项目，不修改任何其他项目。</param>
        /// <param name="correct">客观比较结果。</param>
        /// <param name="ordinal">当前答题题序。</param>
        /// <param name="localDate">实际作答本地日期。</param>
        internal void ApplyAnswer(KanaSessionItemPo item, bool correct, int ordinal, string localDate)
        {
            item.HasWrong |= !correct;
            item.CorrectStreak = correct ? checked(item.CorrectStreak + 1) : 0;
            item.IsCompleted = item.CorrectStreak >= (item.HasWrong ? 2 : 1);
            item.CompletedLocalDate = item.IsCompleted ? localDate : null;
            item.NextEligibleOrdinal = checked(ordinal + random.Next(4, 9));
        }

        /// <summary>优先原队列的到期项目；无足够其它题时提前释放，避免剩余单项死锁。</summary>
        /// <param name="items">本轮项目状态，完成项不会再抽。</param>
        /// <param name="ordinal">即将出题的题序。</param>
        /// <returns>下一项目，所有项目完成时为空。</returns>
        internal KanaSessionItemPo? SelectNext(IReadOnlyList<KanaSessionItemPo> items, int ordinal)
        {
            var remaining = items.Where(item => !item.IsCompleted).ToArray();
            return remaining.Where(item => item.NextEligibleOrdinal <= ordinal).OrderBy(item => item.QueueOrder).FirstOrDefault()
                ?? remaining.OrderBy(item => item.NextEligibleOrdinal).ThenBy(item => item.QueueOrder).FirstOrDefault();
        }

        /// <summary>对已经确定范围的项目随机排题，不改变风险选入结果。</summary>
        /// <param name="symbols">本轮已按范围和每日额度选定的项目。</param>
        /// <returns>独立数组中的随机顺序。</returns>
        internal KanaSymbol[] Shuffle(IReadOnlyList<KanaSymbol> symbols)
        {
            var result = symbols.ToArray();
            random.Shuffle(result);
            return result;
        }
    }
}
