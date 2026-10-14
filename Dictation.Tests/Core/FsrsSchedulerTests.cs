using Dictation.Core.Review;

namespace Dictation.Tests.Core
{
    /// <summary>将最小 FSRS 内核与固定版本的官方 Scheduler 黄金向量及输入边界逐项核对。</summary>
    public sealed class FsrsSchedulerTests
    {
        private static readonly DateTimeOffset Start = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        private readonly FsrsScheduler scheduler = new();

        [Theory]
        [InlineData(new double[] { 0 }, new bool[] { true }, 2.3065, 2.118103970459016, 2, 0)]
        [InlineData(new double[] { 0 }, new bool[] { false }, 0.212, 6.4133, 1, 1)]
        [InlineData(new double[] { 0, 2 }, new bool[] { true, true }, 10.964332335820698, 2.111214235785395, 11, 0)]
        [InlineData(new double[] { 0, 7 }, new bool[] { true, false }, 0.7178527669042931, 7.394502741279718, 1, 1)]
        [InlineData(new double[] { 0, 0.5 }, new bool[] { true, false }, 0.7750839828558984, 7.394502741279718, 1, 1)]
        [InlineData(new double[] { 0, 0.5 }, new bool[] { false, true }, 0.24668918777567272, 6.402115069296838, 1, 1)]
        [InlineData(new double[] { 0, 2, 8, 30 }, new bool[] { true, true, true, false }, 2.452929475623042, 7.389975788014609, 2, 1)]
        public void Schedule_OfficialGoldenSequenceMatchesValuesAndDueDate(
            double[] days, bool[] results, double stability, double difficulty, int interval, int lapses)
        {
            FsrsMemoryState? state = null;
            for (var index = 0; index < days.Length; index++)
            {
                state = scheduler.Schedule(state, results[index], Start.AddDays(days[index]));
            }
            Assert.NotNull(state);
            Assert.Equal(stability, state.Stability, precision: 10);
            Assert.Equal(difficulty, state.Difficulty, precision: 10);
            Assert.Equal(Start.AddDays(days[^1] + interval), state.DueAtUtc);
            Assert.Equal(Start.AddDays(days[^1]), state.LastReviewedAtUtc);
            Assert.Equal(days.Length, state.ReviewCount);
            Assert.Equal(lapses, state.LapseCount);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0.5, 1)]
        [InlineData(2, 0.9094932559773545)]
        [InlineData(7.9, 0.8083099402375552)]
        public void Retrievability_OfficialGoldenValuesUseElapsedWholeDays(double days, double expected)
        {
            var state = ValidState();
            Assert.Equal(expected, scheduler.GetRetrievability(state, Start.AddDays(days)), precision: 12);
        }

        [Fact]
        public void Retrievability_UnknownStateIsNotPretendedLearned()
        {
            Assert.Equal(0, scheduler.GetRetrievability(null, Start));
        }

        [Fact]
        public void Schedule_ProducesNewStateWithoutChangingOriginal()
        {
            var previous = ValidState();
            var original = previous with { };
            var result = scheduler.Schedule(previous, false, Start.AddDays(7));
            Assert.NotSame(previous, result);
            Assert.Equal(original, previous);
            Assert.Equal(2, result.ReviewCount);
            Assert.Equal(1, result.LapseCount);
        }

        [Fact]
        public void Schedule_NormalizesTimeZoneWithoutChangingInstant()
        {
            var result = scheduler.Schedule(null, true, Start.ToOffset(TimeSpan.FromHours(8)));
            Assert.Equal(TimeSpan.Zero, result.LastReviewedAtUtc.Offset);
            Assert.Equal(TimeSpan.Zero, result.DueAtUtc.Offset);
            Assert.Equal(Start.AddDays(2), result.DueAtUtc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Schedule_RejectsInvalidStability(double stability)
        {
            var state = ValidState() with { Stability = stability };
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(state, true, Start.AddDays(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.GetRetrievability(state, Start.AddDays(2)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Schedule_RejectsInvalidDifficulty(double difficulty)
        {
            var state = ValidState() with { Difficulty = difficulty };
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(state, true, Start.AddDays(2)));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, -1)]
        [InlineData(1, 2)]
        public void Schedule_RejectsInvalidCounters(int reviews, int lapses)
        {
            var state = ValidState() with { ReviewCount = reviews, LapseCount = lapses };
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(state, true, Start.AddDays(2)));
        }

        [Fact]
        public void Schedule_RejectsBackwardsReviewButDisplayClampsElapsedTime()
        {
            var state = ValidState();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(state, true, Start.AddSeconds(-1)));
            Assert.Equal(1, scheduler.GetRetrievability(state, Start.AddSeconds(-1)));
        }

        [Fact]
        public void Schedule_RejectsImpossibleDueDate()
        {
            var state = ValidState() with { DueAtUtc = Start };
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(state, true, Start.AddDays(2)));
        }

        [Fact]
        public void Schedule_ClampsIntervalToOfficialMaximum()
        {
            var state = ValidState() with { Stability = 100000 };
            var now = Start.AddDays(2);
            var result = scheduler.Schedule(state, true, now);
            Assert.Equal(now.AddDays(36500), result.DueAtUtc);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(10, false)]
        public void Schedule_ValidExtremeDifficultyRemainsInRange(double difficulty, bool correct)
        {
            var result = scheduler.Schedule(ValidState() with { Difficulty = difficulty }, correct, Start.AddDays(2));
            Assert.InRange(result.Difficulty, 1, 10);
            Assert.True(double.IsFinite(result.Stability));
            Assert.True(result.Stability > 0);
        }

        private static FsrsMemoryState ValidState() => new(
            2.3065, 2.118103970459016, Start, Start.AddDays(2), 1, 0);
    }
}
