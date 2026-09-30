namespace Dictation.Core.Review
{
    /// <summary>以官方 FSRS 6 默认参数计算客观 Good／Again 的下次到期和预计保持率。</summary>
    internal sealed class FsrsScheduler
    {
        // 数值移植自 Open Spaced Repetition py-fsrs；固定来源与 MIT 归属见 FsrsReference.md 及随包许可证。
        private static readonly double[] weights =
        {
            0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194,
            0.001, 1.8722, 0.1666, 0.796, 1.4835, 0.0614, 0.2629,
            1.6483, 0.6014, 1.8729, 0.5425, 0.0912, 0.0658, 0.1542
        };
        private static readonly double decay = -weights[20];
        private static readonly double factor = Math.Pow(0.9, 1 / decay) - 1;

        /// <summary>按一次会话的最终客观结果生成新记忆状态，不修改旧状态。</summary>
        /// <param name="previous">以前的记忆状态；首次评价为空。</param>
        /// <param name="wasCorrect">该会话从未答错为 Good，否则为 Again。</param>
        /// <param name="reviewedAtUtc">本次评价时间。</param>
        /// <returns>稳定度、难度、计数及 UTC 下次复习时间。</returns>
        internal FsrsMemoryState Schedule(FsrsMemoryState? previous, bool wasCorrect, DateTimeOffset reviewedAtUtc)
        {
            var now = reviewedAtUtc.ToUniversalTime();
            var rating = wasCorrect ? 3 : 1;
            double stability;
            double difficulty;
            if (previous is null)
            {
                stability = weights[rating - 1];
                difficulty = Math.Clamp(InitialDifficulty(rating), 1, 10);
            }
            else
            {
                ValidateState(previous);
                if (now < previous.LastReviewedAtUtc)
                {
                    throw new ArgumentOutOfRangeException(nameof(reviewedAtUtc), "评价时间不能早于上次评价。");
                }
                var elapsedDays = Math.Floor((now - previous.LastReviewedAtUtc).TotalDays);
                stability = NextStability(previous, rating, elapsedDays, now);
                var change = -weights[6] * (rating - 3);
                var damped = previous.Difficulty + (10 - previous.Difficulty) * change / 9;
                difficulty = Math.Clamp(weights[7] * InitialDifficulty(4) + (1 - weights[7]) * damped, 1, 10);
            }

            stability = Math.Max(stability, 0.001);
            var interval = stability / factor * (Math.Pow(0.9, 1 / decay) - 1);
            var intervalDays = (int)Math.Clamp(Math.Round(interval, MidpointRounding.ToEven), 1, 36500);
            return new FsrsMemoryState(
                stability, difficulty, now, now.AddDays(intervalDays),
                checked((previous?.ReviewCount ?? 0) + 1),
                checked((previous?.LapseCount ?? 0) + (wasCorrect ? 0 : 1)));
        }

        /// <summary>根据距上次评价的完整天数计算当前预计保持率。</summary>
        /// <param name="state">已有记忆状态；为空表示尚未测试。</param>
        /// <param name="now">需要查询保持率的时间。</param>
        /// <returns>0–1 的预测值；没有状态时为 0。</returns>
        internal double GetRetrievability(FsrsMemoryState? state, DateTimeOffset now)
        {
            if (state is null)
            {
                return 0;
            }
            ValidateState(state);
            // 与参考 .days 一致；展示时的时钟倒退只将经过时间视为零，不伪造负遗忘。
            var elapsedDays = Math.Max(0, Math.Floor((now - state.LastReviewedAtUtc).TotalDays));
            return Math.Pow(1 + factor * elapsedDays / state.Stability, decay);
        }

        /// <summary>根据初次评价等级计算未钳制难度，供初始状态与均值回归共同使用。</summary>
        /// <param name="rating">参考算法中的等级数字。</param>
        /// <returns>初始难度；调用方按自身公式进行 1–10 钳制。</returns>
        private static double InitialDifficulty(int rating) => weights[4] - Math.Exp(weights[5] * (rating - 1)) + 1;

        /// <summary>分别处理同日短期记忆、长期遗忘和长期成功回忆，保持官方计算次序。</summary>
        /// <param name="state">评价前的记忆状态。</param>
        /// <param name="rating">Good 为 3，Again 为 1。</param>
        /// <param name="elapsedDays">距上次评价的完整天数。</param>
        /// <param name="now">当前评价时间。</param>
        /// <returns>尚未应用稳定度最小值钳制的新稳定度。</returns>
        private double NextStability(FsrsMemoryState state, int rating, double elapsedDays, DateTimeOffset now)
        {
            if (elapsedDays < 1)
            {
                var increase = Math.Exp(weights[17] * (rating - 3 + weights[18]))
                    * Math.Pow(state.Stability, -weights[19]);
                return state.Stability * (rating == 3 ? Math.Max(increase, 1) : increase);
            }

            var retrievability = GetRetrievability(state, now);
            if (rating == 1)
            {
                var longTerm = weights[11] * Math.Pow(state.Difficulty, -weights[12])
                    * (Math.Pow(state.Stability + 1, weights[13]) - 1)
                    * Math.Exp((1 - retrievability) * weights[14]);
                var shortTerm = state.Stability / Math.Exp(weights[17] * weights[18]);
                return Math.Min(longTerm, shortTerm);
            }

            return state.Stability * (1 + Math.Exp(weights[8]) * (11 - state.Difficulty)
                * Math.Pow(state.Stability, -weights[9])
                * (Math.Exp((1 - retrievability) * weights[10]) - 1));
        }

        /// <summary>拒绝损坏数值和不一致的计数／时间，避免 NaN 或非法状态进入持久化链路。</summary>
        /// <param name="state">待使用的已有状态。</param>
        private static void ValidateState(FsrsMemoryState state)
        {
            if (!double.IsFinite(state.Stability) || state.Stability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(state.Stability), "稳定度必须是有限正数。");
            }
            if (!double.IsFinite(state.Difficulty) || state.Difficulty < 1 || state.Difficulty > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(state.Difficulty), "难度必须是 1–10 的有限数值。");
            }
            ArgumentOutOfRangeException.ThrowIfLessThan(state.ReviewCount, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(state.LapseCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(state.LapseCount, state.ReviewCount);
            if (state.DueAtUtc <= state.LastReviewedAtUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(state.DueAtUtc), "到期时间必须晚于上次评价。");
            }
        }
    }
}
