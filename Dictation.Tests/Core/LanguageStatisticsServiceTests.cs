using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Mistakes;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证真实词组统计、错误来源，以及复用 FSRS 的只读预测。</summary>
    public sealed class LanguageStatisticsServiceTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task Load_ImportedOldWordIsPendingNotLearnedOrPerfectMemory()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await env.ImportAsync(decision: ImportDecision.Review);
            var result = await env.Provider.GetRequiredService<ILanguageStatisticsReader>().LoadAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(0, result.LearnedGroupCount);
            Assert.Equal(1, result.PendingTestGroupCount);
            Assert.Equal(0, result.OverdueGroupCount);
            Assert.Empty(result.RetentionTrend);
            Assert.Empty(result.TopMistakes);
        }

        [Fact]
        public async Task Load_CompletedFormsCountOneGroupAndDeletedGroupDisappears()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var id = await env.ImportAsync();
            await env.SavePlanAsync("Dictionary", "Polite");
            await env.FinishAsync("Polite");
            var reader = env.Provider.GetRequiredService<ILanguageStatisticsReader>();
            var result = await reader.LoadAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(1, result.LearnedGroupCount);
            Assert.Equal(1, result.TodayLearnedGroupCount);
            Assert.Equal(1, Assert.Single(result.TopMistakes).ErrorCount);
            await env.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, id, Token);
            result = await reader.LoadAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(0, result.LearnedGroupCount);
            Assert.Empty(result.TopMistakes);
            Assert.Empty(result.RetentionTrend);
        }

        [Fact]
        public async Task Load_SharedDictationCountsEachGroupErrorOnce()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await env.ImportAsync();
            await env.ImportAsync("食べる", "jmdict:1:2");
            await env.Provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), Token);
            var session = await env.Sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
            var question = Assert.IsType<PracticeQuestionDto>(await env.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await env.Sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "错误"), Token);
            var result = await env.Provider.GetRequiredService<ILanguageStatisticsReader>().LoadAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(2, result.TopMistakes.Count);
            Assert.All(result.TopMistakes, item => Assert.Equal(1, item.ErrorCount));
            Assert.Equal(2, result.TopMistakes.Sum(item => item.ErrorCount));
        }

        [Fact]
        public async Task Load_ForecastHasThirtyMonotonePointsAndGroupWeightedWeakestForms()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var result = await Reader(host, new[] { Group("a", Memory("base", 1), Memory("past", 100)), Group("b", Memory("base", 100)) })
                .LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(30, result.RetentionTrend.Count);
            Assert.Equal(new DateOnly(2026, 10, 12), result.RetentionTrend[0].Date);
            Assert.Equal(new DateOnly(2026, 11, 10), result.RetentionTrend[^1].Date);
            Assert.All(result.RetentionTrend, point => Assert.InRange(point.Value, 0, 1));
            Assert.True(result.RetentionTrend[0].Value < 0.96); // 不能按三个词形平均，让弱组被稀释。
            for (var index = 1; index < 30; index++) Assert.True(result.RetentionTrend[index].Value <= result.RetentionTrend[index - 1].Value);
            Assert.True(result.RetentionTrend[^1].Value < result.RetentionTrend[0].Value);
        }

        [Fact]
        public async Task Load_UnassessedFormDoesNotFabricateMemoryOrChangeAssessedForecast()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var assessed = Memory("base", 10);
            var empty = new ReviewFormMemoryDto("a", "go", "past", "过去式", false, false, null, null, null, null, null, 0, 0);
            var result = await Reader(host, new[] { Group("a", assessed, empty) }).LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(0, result.PendingTestGroupCount);
            Assert.Equal(30, result.RetentionTrend.Count);
            Assert.InRange(result.RetentionTrend[0].Value, 0.97, 1);
        }

        [Fact]
        public async Task Load_DueTrendBucketsEarliestFormOnceAndSeparatesOverdueAndPending()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var result = await Reader(host, new[] {
                Group("a", Memory("base", 10, 2), Memory("past", 10, 4)),
                Group("b", Memory("base", 1, -0.2)),
                Group("c"), Group("d", Memory("base", 10, 20)) }).LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(14, result.DueTrend.Count);
            Assert.Equal(1, result.DueTrend[2].Value);
            Assert.Equal(1, result.DueTrend.Sum(point => point.Value));
            Assert.Equal(1, result.PendingTestGroupCount);
            Assert.Equal(1, result.OverdueGroupCount);
        }

        [Fact]
        public async Task Load_UsesLocalDayForDueBuckets()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var memory = Memory("base", 10) with { DueAtUtc = new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.Zero) };
            var result = await Reader(host, new[] { Group("a", memory) }, zone: TimeZoneInfo.CreateCustomTimeZone("test+8", TimeSpan.FromHours(8), "test", "test"))
                .LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(14, result.DueTrend.Count);
            Assert.Equal(new DateOnly(2026, 10, 13), result.DueTrend[1].Date);
            Assert.Equal(1, result.DueTrend[1].Value);
            Assert.Equal(0, result.DueTrend[0].Value);
        }

        [Fact]
        public async Task Load_TopTenAggregatesFormsAndSortsTiesByGroupId()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var errors = Enumerable.Range(0, 12).Reverse().Select(i => Error($"g{i:00}", "base", 1)).ToList();
            errors.Add(Error("g11", "past", 2));
            var result = await Reader(host, Array.Empty<ReviewWordGroupDto>(), errors).LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(10, result.TopMistakes.Count);
            Assert.Equal("g11", result.TopMistakes[0].WordGroupId);
            Assert.Equal(3, result.TopMistakes[0].ErrorCount);
            Assert.Equal("g00", result.TopMistakes[1].WordGroupId);
            Assert.Equal("移动", result.TopMistakes[0].Meanings);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        public async Task Load_CorruptMemoryDoesNotBecomeSuccessfulChart(double stability)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Reader(host, new[] { Group("a", Memory("base", stability)) }).LoadAsync(LearningLanguage.English, Token));
        }

        [Fact]
        public async Task Load_UsesOneLibrarySnapshotForCountsAndBothTrends()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var library = new ControlledLibrary(new[] { Group("a", Memory("base", 10)) });
            var result = await Reader(host, library.Groups, library: library).LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(30, result.RetentionTrend.Count);
            Assert.Equal(14, result.DueTrend.Count);
            Assert.Equal(1, library.LoadCount);
        }

        [Fact]
        public async Task Load_RejectsInvalidLanguageAndCancellation()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var reader = Reader(host, Array.Empty<ReviewWordGroupDto>());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.LoadAsync((LearningLanguage)99, Token));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.LoadAsync(LearningLanguage.English, canceled.Token));
        }

        [Fact]
        public async Task Load_ActualCompletedReviewIsNotInflatedByRetry()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedReviewAsync("a", "first", "English");
            await host.SeedReviewAsync("a", "second", "English");
            var result = await Reader(host, Array.Empty<ReviewWordGroupDto>()).LoadAsync(LearningLanguage.English, Token);
            Assert.Equal(1, result.TodayReviewedGroupCount);
            Assert.Equal(0, result.LearnedGroupCount);
        }

        private static LanguageStatisticsService Reader(StatisticsTestHost host, IReadOnlyList<ReviewWordGroupDto> groups,
            IReadOnlyList<WrongAnswerItemDto>? errors = null, TimeZoneInfo? zone = null, ControlledLibrary? library = null) => new(
                host.Services.GetRequiredService<LearningStatisticsDataAccessor>(), host.Services.GetRequiredService<DailyCheckInDataAccessor>(),
                library ?? new ControlledLibrary(groups), new ControlledErrors(errors ?? Array.Empty<WrongAnswerItemDto>()), new FsrsScheduler(), new ReportClock(zone));

        private static ReviewWordGroupDto Group(string id, params ReviewFormMemoryDto[] forms) => new(
            new ImportWordGroupDto(id, LearningLanguage.English, id, ImportWordGroupPlacement.Review, Now, Array.Empty<ImportWordGroupMemberDto>()),
            forms, forms.Length == 0, false, null, null);
        private static ReviewFormMemoryDto Memory(string form, double stability, double dueDays = 2) => new("a", "go", form, "词形", true, false,
            stability, 5, Now.AddDays(-1), Now.AddDays(dueDays), null, 1, 0);
        private static WrongAnswerItemDto Error(string id, string form, int count) => new(id, id, form, "go", "词形", "移动", "Review", null,
            Enumerable.Range(0, count).Select(i => new WrongAnswerAttemptDto("Review", LearningChannel.Dictation, LearningOutputMode.Latin, "bad", Now.AddSeconds(i), "go", "go")).ToArray());

        private sealed class ControlledLibrary(IReadOnlyList<ReviewWordGroupDto> groups) : IReviewLibraryQuery
        {
            internal IReadOnlyList<ReviewWordGroupDto> Groups => groups;
            internal int LoadCount { get; private set; }
            public Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++LoadCount > 1) throw new InvalidOperationException("不允许为第二张图重复读取词库。");
                return Task.FromResult(new ReviewLibraryDto(language, Now, groups));
            }
        }
        private sealed class ControlledErrors(IReadOnlyList<WrongAnswerItemDto> errors) : IWrongAnswerQuery
        {
            public Task<IReadOnlyList<WrongAnswerItemDto>> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.FromResult(errors);
        }
        private sealed class ReportClock(TimeZoneInfo? zone) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
            public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;
        }
    }
}
