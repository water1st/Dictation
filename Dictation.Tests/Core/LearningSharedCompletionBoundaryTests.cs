using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证共享听写不能跳过独立覆盖或阻止尚需精确纠错的合法成绩。</summary>
    public sealed class LearningSharedCompletionBoundaryTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Submit_MixedChannelsAndScriptsRequiresCoverageAndRetainsUnresolvedRetries(bool firstTwoWrong)
        {
            var token = TestContext.Current.CancellationToken;
            // 固定有限随机种子覆盖真实调度分支，不手工修改题目或成绩以制造不可达状态。
            for (var seed = 0; seed < 16; seed++)
            {
                using var directory = new TemporaryDirectory();
                using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path,
                    configure: services => services.AddSingleton(new Random(seed)));
                foreach (var sense in new[] { "jmdict:1:1", "jmdict:1:2" })
                {
                    var batch = await provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.Japanese,
                        ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", sense) }), token);
                    await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(batch.Batch.BatchId,
                        new[] { Assert.Single(batch.Batch.Items).ItemId }, ImportDecision.Learning), token);
                }
                await provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                    LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                    new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                    new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }), token);
                var sessions = provider.GetRequiredService<LearningSessionService>();
                var session = await sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), token);
                var question = Assert.IsType<PracticeQuestionDto>(await sessions.GetCurrentQuestionAsync(session.SessionId, token));
                LearningSessionSummaryDto? summary = null;
                for (var ordinal = 0; ordinal < 30 && summary is null; ordinal++)
                {
                    var text = firstTwoWrong && ordinal < 2 ? "错误"
                        : question.OutputMode == LearningOutputMode.Kanji ? "食べる" : "たべる";
                    LearningAnswerResultDto? result = null;
                    var failure = await Record.ExceptionAsync(async () =>
                        result = await sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, text), token));
                    Assert.True(failure is null, $"种子{seed}，题序{ordinal}，{question.Channel}/{question.OutputMode}：{failure}");
                    await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
                    await connection.OpenAsync(token);
                    var premature = await connection.ExecuteScalarAsync<int>("""
                        SELECT COUNT(*) FROM LearningFormStates WHERE IsMastered=1
                          AND (MeaningWritingCount=0 OR DictationCount=0 OR KanaCount=0 OR KanjiCount=0);
                        """);
                    Assert.True(premature == 0, $"种子{seed}，题序{ordinal}：未覆盖全部启用渠道及字形的义项被提前掌握。");
                    summary = result!.Summary;
                    if (summary is null) question = Assert.IsType<PracticeQuestionDto>(result.NextQuestion);
                }
                Assert.NotNull(summary);
                Assert.True(summary.AnsweredCount >= 3);
                Assert.Equal(firstTwoWrong, summary.Errors.Count > 0);
            }
        }
    }
}
