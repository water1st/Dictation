using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证独立复习计划的语言约束、例词解析和整组管理，不以学习设置代替复习设置。</summary>
    public sealed class ReviewPlanServiceTests
    {
        [Fact]
        public async Task Load_IndependentDefaultsDoNotWriteDatabase()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var reader = environment.Provider.GetRequiredService<IReviewPlanReader>();
            var japanese = await reader.LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            var english = await reader.LoadAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            Assert.Equal(20, japanese.DailyReviewTarget);
            Assert.Equal(20, japanese.SessionWordLimit);
            Assert.Equal(ReviewRangeMode.All, japanese.RangeMode);
            Assert.Empty(japanese.WordGroupIds);
            Assert.Equal(new[] { "Dictionary" }, japanese.FormCodes);
            Assert.Equal(new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }, japanese.OutputModes);
            Assert.Equal(new[] { "Base" }, english.FormCodes);
            Assert.Equal(new[] { LearningOutputMode.Latin }, english.OutputModes);
            Assert.Equal(new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation }, english.Channels);
            Assert.Null(english.EnglishExampleParadigm);
            Assert.Equal(MemorySeedTestEnvironment.Now, english.UpdatedAtUtc);
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewPlans;"));
        }

        [Fact]
        public async Task Save_NormalizesAndSeparatesLanguagesWithoutChangingLearningOrMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            var before = Assert.Single(await environment.MemoriesAsync(group));
            var editor = environment.Provider.GetRequiredService<IReviewPlanEditor>();
            var saved = await editor.SaveAsync(Japanese() with
            {
                DailyReviewTarget = 35,
                SessionWordLimit = 12,
                RangeMode = ReviewRangeMode.Selected,
                WordGroupIds = new[] { " " + group + " ", group },
                FormCodes = new[] { " Dictionary ", "Dictionary", "Te" },
                Channels = new[] { LearningChannel.Dictation, LearningChannel.Dictation },
                OutputModes = new[] { LearningOutputMode.Kana, LearningOutputMode.Kana }
            }, TestContext.Current.CancellationToken);
            var english = await editor.SaveAsync(English() with { DailyReviewTarget = 7 }, TestContext.Current.CancellationToken);
            var reader = environment.Provider.GetRequiredService<IReviewPlanReader>();
            var reloaded = await reader.LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(35, reloaded.DailyReviewTarget);
            Assert.Equal(12, reloaded.SessionWordLimit);
            Assert.Equal(new[] { group }, reloaded.WordGroupIds);
            Assert.Equal(new[] { "Dictionary", "Te" }, reloaded.FormCodes);
            Assert.Equal(new[] { LearningChannel.Dictation }, reloaded.Channels);
            Assert.Equal(new[] { LearningOutputMode.Kana }, reloaded.OutputModes);
            Assert.Equal(saved.UpdatedAtUtc, reloaded.UpdatedAtUtc);
            Assert.Equal(7, english.DailyReviewTarget);
            var learning = await environment.Provider.GetRequiredService<ILearningPlanReader>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(20, learning.DailyNewTarget);
            Assert.Equal(new[] { "Dictionary" }, learning.FormCodes);
            var after = Assert.Single(await environment.MemoriesAsync(group));
            Assert.Equal((double)before.Stability, (double)after.Stability);
            Assert.Equal((long)before.ReviewCount, (long)after.ReviewCount);
            Assert.Equal((string)before.DueAtUtc, (string)after.DueAtUtc);
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
        [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
        [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
        [InlineData(16)] [InlineData(17)] [InlineData(18)] [InlineData(19)]
        [InlineData(20)] [InlineData(21)] [InlineData(22)] [InlineData(23)]
        public async Task Save_InvalidOptionsRejectWholeRequest(int scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var request = scenario switch
            {
                0 => Japanese() with { DailyReviewTarget = 0 },
                1 => Japanese() with { DailyReviewTarget = 501 },
                2 => Japanese() with { SessionWordLimit = 0 },
                3 => Japanese() with { SessionWordLimit = 501 },
                4 => Japanese() with { Language = (LearningLanguage)99 },
                5 => Japanese() with { RangeMode = (ReviewRangeMode)99 },
                6 => Japanese() with { FormCodes = Array.Empty<string>() },
                7 => Japanese() with { FormCodes = null! },
                8 => Japanese() with { FormCodes = new[] { "Past" } },
                9 => Japanese() with { Channels = Array.Empty<LearningChannel>() },
                10 => Japanese() with { Channels = null! },
                11 => Japanese() with { Channels = new[] { (LearningChannel)99 } },
                12 => Japanese() with { OutputModes = Array.Empty<LearningOutputMode>() },
                13 => Japanese() with { OutputModes = null! },
                14 => Japanese() with { OutputModes = new[] { LearningOutputMode.Latin } },
                15 => Japanese() with { OutputModes = new[] { (LearningOutputMode)99 } },
                16 => Japanese() with { EnglishFormLabelMode = (EnglishFormLabelMode)99 },
                17 => Japanese() with { EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm },
                18 => Japanese() with { EnglishExampleSourceSenseId = "go%2:01:00::" },
                19 => Japanese() with { RangeMode = ReviewRangeMode.Selected },
                20 => Japanese() with { WordGroupIds = null! },
                21 => Japanese() with { RangeMode = ReviewRangeMode.Selected, WordGroupIds = new[] { " " } },
                22 => English() with { FormCodes = new[] { "Dictionary" } },
                _ => English() with { OutputModes = new[] { LearningOutputMode.Latin, LearningOutputMode.Kanji } }
            };
            await Assert.ThrowsAnyAsync<ArgumentException>(() => environment.Provider.GetRequiredService<IReviewPlanEditor>()
                .SaveAsync(request, TestContext.Current.CancellationToken));
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewPlans;"));
        }

        [Fact]
        public async Task Save_EnglishExampleUsesItsOwnFormsAndGrammarModeClearsExample()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var editor = environment.Provider.GetRequiredService<IReviewPlanEditor>();
            await editor.SaveAsync(English() with
            {
                EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm,
                EnglishExampleSourceSenseId = " go%2:01:00:: ",
                FormCodes = new[] { "Base", "Past", "PastParticiple" }
            }, TestContext.Current.CancellationToken);
            var loaded = await environment.Provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            var example = Assert.IsType<EnglishExampleParadigmDto>(loaded.EnglishExampleParadigm);
            Assert.Equal("go", example.Headword);
            Assert.Equal(new[] { "go", "went", "gone" }, example.Forms.Select(form => form.Surface));
            var reset = await editor.SaveAsync(English() with { EnglishExampleSourceSenseId = "unused" }, TestContext.Current.CancellationToken);
            Assert.Null(reset.EnglishExampleSourceSenseId);
            Assert.Null(reset.EnglishExampleParadigm);
        }

        [Theory]
        [InlineData(null, "Base")]
        [InlineData("unknown", "Base")]
        [InlineData("conversion%1:04:00::", "Base")]
        [InlineData("take_off%2:38:00::", "Base")]
        [InlineData("go%2:01:00::", "Plural")]
        public async Task Save_MissingOrUnsuitableExampleRejectsWithoutWriting(string? sense, string form)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(
                English() with { EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm, EnglishExampleSourceSenseId = sense, FormCodes = new[] { form } },
                TestContext.Current.CancellationToken));
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewPlans;"));
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("learning")]
        [InlineData("foreign")]
        public async Task Save_InvalidGroupCannotEnterReviewScope(string scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = scenario == "missing" ? "missing" : await environment.ImportAsync(decision: ImportDecision.Learning);
            if (scenario == "foreign")
            {
                await using var connection = environment.Connections.CreateConnection();
                await connection.ExecuteAsync("UPDATE WordGroups SET Language = 'English', Placement = 'Review' WHERE WordGroupId = @Id;", new { Id = group });
            }
            await Assert.ThrowsAnyAsync<ArgumentException>(() => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(
                Japanese() with { RangeMode = ReviewRangeMode.Selected, WordGroupIds = new[] { group } }, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("Load")]
        [InlineData("Save")]
        [InlineData("Delete")]
        public async Task Operations_CanceledBeforeStartWriteNothing(string operation)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
            {
                "Load" => environment.Provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, cancellation.Token),
                "Save" => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Japanese(), cancellation.Token),
                _ => environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, group, cancellation.Token)
            });
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewPlans;"));
        }

        [Fact]
        public async Task Operations_RejectNullRequestUnknownLanguageAndEmptyDeleteId()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentNullException>(() => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(null!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Provider.GetRequiredService<IReviewPlanReader>().LoadAsync((LearningLanguage)99, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, " ", TestContext.Current.CancellationToken));
        }

        private static SaveReviewPlanRequestDto Japanese() => new(LearningLanguage.Japanese, 20, 20, ReviewRangeMode.All,
            Array.Empty<string>(), new[] { "Dictionary" }, new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null);

        private static SaveReviewPlanRequestDto English() => Japanese() with
        {
            Language = LearningLanguage.English, FormCodes = new[] { "Base" }, OutputModes = new[] { LearningOutputMode.Latin }
        };
    }
}
