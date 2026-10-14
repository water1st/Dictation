using Dapper;
using Dictation.Core.Kana;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证同风险选入随机，而只读进度不改变下一次选择的随机序列。</summary>
    public sealed class KanaSelectionBoundaryTests
    {
        [Fact]
        public async Task Start_EqualRiskChangesAcrossSeedsButProgressReadsDoNotChangeSelection()
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            var observed = new HashSet<string>();
            for (var seed = 0; seed < 6; seed++)
            {
                var answers = new List<string>();
                for (var reads = 0; reads < 2; reads++)
                {
                    using var provider = await ReviewPlanPresentationTestHost.CreateAsync(Path.Combine(directory.Path, $"{seed}-{reads}"),
                        createLexicons: false, configure: services => services.AddSingleton(new Random(seed)));
                    await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), token);
                    if (reads != 0)
                        for (var index = 0; index < 3; index++) await provider.GetRequiredService<IKanaProgressReader>().GetProgressAsync(token);
                    var session = (await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token))!;
                    answers.Add((await provider.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token))!.KanaId);
                }
                Assert.Equal(answers[0], answers[1]);
                observed.Add(answers[0]);
            }
            Assert.True(observed.Count > 1, "同风险项目不应始终选入同一个Unicode最前项。");
        }

        [Fact]
        public async Task Start_RandomTieBreakingKeepsLowerRetrievabilityAndUnseenPriority()
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            const string clear = "あ い う え お か き く け こ さ し す せ そ た ち つ て と な に ぬ ね の は ひ ふ へ ほ ま み む め も や ゆ よ ら り る れ ろ わ を ん";
            for (var seed = 0; seed < 4; seed++)
            {
                for (var unseen = 0; unseen < 2; unseen++)
                {
                    using var provider = await ReviewPlanPresentationTestHost.CreateAsync(Path.Combine(directory.Path, $"risk-{seed}-{unseen}"),
                        createLexicons: false, configure: services => services.AddSingleton(new Random(seed)));
                    await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), token);
                    await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
                    await connection.OpenAsync(token);
                    var now = DateTimeOffset.UtcNow;
                    foreach (var text in clear.Split(' '))
                    {
                        if (unseen == 1 && text == "い") continue;
                        var weak = text == "ん";
                        await connection.ExecuteAsync(new CommandDefinition("""
                            INSERT INTO KanaMemories VALUES (@Id,@Stability,5,@Reviewed,@Due,1,0);
                            """, new
                        {
                            Id = $"Hiragana:{text}", Stability = weak ? 1d : 10d,
                            Reviewed = now.AddDays(weak ? -30 : -2).ToString("O"),
                            Due = now.AddDays(weak ? -29 : -1).ToString("O")
                        }, cancellationToken: token));
                    }
                    var progress = await provider.GetRequiredService<IKanaProgressReader>().GetProgressAsync(token);
                    Assert.Equal(46, progress.Available);
                    Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM KanaAttempts;"));
                    var session = (await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token))!;
                    var question = (await provider.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token))!;
                    Assert.Equal(unseen == 1 ? "Hiragana:い" : "Hiragana:ん", question.KanaId);
                }
            }
        }
    }
}
