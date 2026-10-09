using Dapper;
using Dictation.Core.Kana;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实三层DI与SQLite验证五十音范围、客观作答、恢复和独立记忆。</summary>
    public sealed class KanaServiceTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;
        // 独立手工事实夹具，不从产品Catalog计算预期答案。
        private const string clear = "あ い う え お か き く け こ さ し す せ そ た ち つ て と な に ぬ ね の は ひ ふ へ ほ ま み む め も や ゆ よ ら り る れ ろ わ を ん";
        private const string clearKatakana = "ア イ ウ エ オ カ キ ク ケ コ サ シ ス セ ソ タ チ ツ テ ト ナ ニ ヌ ネ ノ ハ ヒ フ ヘ ホ マ ミ ム メ モ ヤ ユ ヨ ラ リ ル レ ロ ワ ヲ ン";
        private const string voiced = "が ぎ ぐ げ ご ざ じ ず ぜ ぞ だ ぢ づ で ど ば び ぶ べ ぼ ぱ ぴ ぷ ぺ ぽ";
        private const string voicedKatakana = "ガ ギ グ ゲ ゴ ザ ジ ズ ゼ ゾ ダ ヂ ヅ デ ド バ ビ ブ ベ ボ パ ピ プ ペ ポ";
        private const string contracted = "きゃ きゅ きょ ぎゃ ぎゅ ぎょ しゃ しゅ しょ じゃ じゅ じょ ちゃ ちゅ ちょ ぢゃ ぢゅ ぢょ にゃ にゅ にょ ひゃ ひゅ ひょ びゃ びゅ びょ ぴゃ ぴゅ ぴょ みゃ みゅ みょ りゃ りゅ りょ";
        private const string contractedKatakana = "キャ キュ キョ ギャ ギュ ギョ シャ シュ ショ ジャ ジュ ジョ チャ チュ チョ ヂャ ヂュ ヂョ ニャ ニュ ニョ ヒャ ヒュ ヒョ ビャ ビュ ビョ ピャ ピュ ピョ ミャ ミュ ミョ リャ リュ リョ";

        [Fact]
        public void Catalog_ContainsBothScriptsAndExactModernGroups()
        {
            Assert.Equal(214, KanaCatalog.Items.Count);
            foreach (var (script, range, text) in new[]
            {
                (KanaScript.Hiragana, KanaRange.Clear, clear), (KanaScript.Katakana, KanaRange.Clear, clearKatakana),
                (KanaScript.Hiragana, KanaRange.Voiced, voiced), (KanaScript.Katakana, KanaRange.Voiced, voicedKatakana),
                (KanaScript.Hiragana, KanaRange.Contracted, contracted), (KanaScript.Katakana, KanaRange.Contracted, contractedKatakana)
            })
                Assert.Equal(text.Split(' ').Order(), KanaCatalog.Items.Where(i => i.Script == script && i.Range == range).Select(i => i.Text).Order());
            Assert.Equal(214, KanaCatalog.Items.Select(i => i.Id).Distinct().Count());
            Assert.Equal("た行", KanaCatalog.Items.Single(i => i.Text == "ぢ").RowHint);
            Assert.Equal("さ行", KanaCatalog.Items.Single(i => i.Text == "じ").RowHint);
            Assert.Equal("わ行", KanaCatalog.Items.Single(i => i.Text == "ヲ").RowHint);
            Assert.Equal("だ行", KanaCatalog.Items.Single(i => i.Text == "ぢゃ").RowHint);
            Assert.Equal("お", KanaCatalog.Items.Single(i => i.Text == "ヲ").Reading);
        }

        [Fact]
        public async Task Plan_DefaultDisabledAndRejectsEmptyDimensionsOrInvalidCounts()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var read = provider.GetRequiredService<IKanaPlanReader>();
            var edit = provider.GetRequiredService<IKanaPlanEditor>();
            var plan = await read.LoadAsync(token);
            Assert.False(plan.Enabled);
            Assert.True(plan.Hiragana && plan.Clear);
            Assert.Null(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            foreach (var invalid in new[] { plan with { Hiragana = false, Katakana = false }, plan with { Clear = false, Voiced = false, Contracted = false }, plan with { DailyTarget = 0 }, plan with { DailyTarget = 215 } })
                await Assert.ThrowsAsync<ArgumentException>(() => edit.SaveAsync(invalid, token));
            await edit.SaveAsync(plan with { Enabled = true, DailyTarget = 214, Katakana = true, Voiced = true, Contracted = true }, token);
            Assert.Equal(214, (await read.LoadAsync(token)).DailyTarget);
            Assert.True((await read.LoadAsync(token)).Enabled);
        }

        [Fact]
        public async Task Start_DailyThreeCreatesOnlyThreeKanaAndNoVocabulary()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, true, false, true, false, false, false, 3));
            var session = Assert.IsType<KanaSessionDto>(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            Assert.Equal(3, session.TotalCount);
            var question = Assert.IsType<KanaQuestionDto>(await provider.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token));
            Assert.Equal(KanaScript.Hiragana, question.Script);
            var progress = await provider.GetRequiredService<IKanaProgressReader>().GetProgressAsync(token);
            Assert.Equal(3, progress.Target);
            Assert.True(progress.HasActiveSession);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM WordGroups;"));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM ReviewFormMemories;"));
        }

        [Fact]
        public async Task Full_AllEnabledClearItemsAppearOnceAndUpdateIndependentMemory()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, true, false, true, false, false, true, 20));
            var session = Assert.IsType<KanaSessionDto>(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            Assert.Equal(46, session.TotalCount);
            var seen = new HashSet<string>();
            var feedback = await CompleteAsync(provider, session, question =>
            {
                Assert.True(seen.Add(question.KanaId));
                return Answer(question);
            });
            Assert.True(feedback.IsCompleted);
            Assert.Equal(46, feedback.CompletedCount);
            Assert.Empty(feedback.Errors);
            Assert.Equal(46L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE ReviewCount=1 AND LapseCount=0;"));
            Assert.Equal(46L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Null(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
        }

        [Fact]
        public async Task Wrong_IsDelayedAndRetryKeepsOneAgainEvenAfterCorrectRecovery()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, true, false, true, false, false, true, 20));
            var session = Assert.IsType<KanaSessionDto>(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            var reader = provider.GetRequiredService<IKanaSessionReader>();
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var first = Assert.IsType<KanaQuestionDto>(await reader.GetCurrentQuestionAsync(session.SessionId, token));
            var error = await operations.SubmitAsync(session.SessionId, first.QuestionOrdinal, "错误", token);
            Assert.False(error.IsCorrect);
            Assert.Equal(Answer(first), error.CorrectAnswer);
            Assert.NotEqual(first.KanaId, error.NextQuestion!.KanaId);
            var final = await CompleteAsync(provider, session, question =>
            {
                if (question.KanaId == first.KanaId) Assert.True(question.QuestionOrdinal >= 4);
                return Answer(question);
            });
            Assert.True(final.IsCompleted);
            Assert.Single(final.Errors);
            Assert.Equal(48L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE ReviewCount=1 AND LapseCount=1;"));
            Assert.Equal(46L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories;"));
        }

        [Fact]
        public async Task Katakana_RejectsHiraganaAndAcceptsDecomposedDakuten()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, false, true, false, true, false, false, 1));
            var session = Assert.IsType<KanaSessionDto>(await provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            var question = Assert.IsType<KanaQuestionDto>(await provider.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token));
            Assert.Equal(KanaScript.Katakana, question.Script);
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var wrong = await operations.SubmitAsync(session.SessionId, question.QuestionOrdinal, question.SpokenText, token);
            Assert.False(wrong.IsCorrect);
            var next = Assert.IsType<KanaQuestionDto>(wrong.NextQuestion);
            var good = await operations.SubmitAsync(session.SessionId, next.QuestionOrdinal, Answer(next).Normalize(System.Text.NormalizationForm.FormD), token);
            Assert.True(good.IsCorrect);
            Assert.False(good.IsCompleted);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories;"));
            var final = await operations.SubmitAsync(session.SessionId, good.NextQuestion!.QuestionOrdinal, Answer(good.NextQuestion), token);
            Assert.True(final.IsCompleted);
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE LapseCount=1;"));
        }

        [Fact]
        public async Task Resume_PersistsOriginalScopeAcrossProviderAndNewPlan()
        {
            using var directory = new TemporaryDirectory();
            string id;
            string firstId;
            using (var original = await CreateAsync(directory.Path))
            {
                await SaveAsync(original, new(true, true, false, true, false, false, false, 3));
                var session = Assert.IsType<KanaSessionDto>(await original.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
                id = session.SessionId;
                firstId = (await original.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(id, token))!.KanaId;
                await SaveAsync(original, new(false, false, true, false, true, false, true, 25));
            }
            using var reopened = await CreateAsync(directory.Path);
            var resumed = Assert.IsType<KanaSessionDto>(await reopened.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
            Assert.Equal(id, resumed.SessionId);
            Assert.Equal(3, resumed.TotalCount);
            Assert.Equal(firstId, (await reopened.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(id, token))!.KanaId);
        }

        [Fact]
        public async Task Rollover_ActiveResumesAndCompletionCountsActualLocalDate()
        {
            using var directory = new TemporaryDirectory();
            var clock = new Clock { Now = DateTimeOffset.Parse("2026-10-08T15:50:00Z") };
            using var provider = await CreateAsync(directory.Path, clock);
            await SaveAsync(provider, new(true, true, false, true, false, false, false, 1));
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var session = Assert.IsType<KanaSessionDto>(await operations.StartOrResumeAsync(token));
            Assert.Equal("2026-10-08", session.StartedLocalDate);
            clock.Now = clock.Now.AddMinutes(20);
            Assert.Equal(session.SessionId, (await operations.StartOrResumeAsync(token))!.SessionId);
            await CompleteAsync(provider, session, Answer);
            Assert.Equal(1, (await provider.GetRequiredService<IKanaProgressReader>().GetProgressAsync(token)).CompletedToday);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaSessionItems WHERE CompletedLocalDate='2026-10-08';"));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaSessionItems WHERE CompletedLocalDate='2026-10-09';"));
        }

        [Fact]
        public async Task Daily_ExcludesFutureMemoriesInsteadOfFillingQuota()
        {
            using var directory = new TemporaryDirectory();
            var clock = new Clock { Now = DateTimeOffset.Parse("2026-10-08T15:50:00Z") };
            using var provider = await CreateAsync(directory.Path, clock);
            await SaveAsync(provider, new(true, true, false, true, false, false, true, 3));
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var session = Assert.IsType<KanaSessionDto>(await operations.StartOrResumeAsync(token));
            await CompleteAsync(provider, session, Answer);
            clock.Now = clock.Now.AddMinutes(20);
            await SaveAsync(provider, new(true, true, false, true, false, false, false, 3));
            Assert.Null(await operations.StartOrResumeAsync(token));
            var progress = await provider.GetRequiredService<IKanaProgressReader>().GetProgressAsync(token);
            Assert.Equal(0, progress.Target);
            Assert.Equal(0, progress.CompletedToday);
        }

        [Fact]
        public async Task Submit_RejectsStaleOrdinalAndCancellationWithoutExtraAttempts()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, true, false, true, false, false, false, 1));
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var reader = provider.GetRequiredService<IKanaSessionReader>();
            var session = Assert.IsType<KanaSessionDto>(await operations.StartOrResumeAsync(token));
            var question = (await reader.GetCurrentQuestionAsync(session.SessionId, token))!;
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.SubmitAsync(session.SessionId, 0, Answer(question), cancelled.Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.SubmitAsync(session.SessionId, 99, Answer(question), token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetCurrentQuestionAsync("missing", token));
            await Assert.ThrowsAsync<ArgumentException>(() => operations.SubmitAsync(" ", 0, "あ", token));
            await operations.SubmitAsync(session.SessionId, question.QuestionOrdinal, Answer(question), token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.SubmitAsync(session.SessionId, question.QuestionOrdinal, Answer(question), token));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT SUM(ReviewCount) FROM KanaMemories;"));
        }

        [Fact]
        public async Task Submit_ConcurrentSameOrdinalHasOnlyOneCommittedWinner()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SaveAsync(provider, new(true, true, false, true, false, false, false, 1));
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var session = (await operations.StartOrResumeAsync(token))!;
            var question = (await provider.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token))!;
            async Task<bool> TrySubmit()
            {
                try { await operations.SubmitAsync(session.SessionId, 0, Answer(question), token); return true; }
                catch (InvalidOperationException) { return false; }
            }
            var results = await Task.WhenAll(Task.Run(TrySubmit, token), Task.Run(TrySubmit, token));
            Assert.Single(results, result => result);
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT SUM(ReviewCount) FROM KanaMemories;"));
        }

        [Fact]
        public async Task Read_BrokenStoragePropagatesInsteadOfPretendingEmpty()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(token);
            await connection.ExecuteAsync("DROP TABLE KanaSessions;");
            await Assert.ThrowsAsync<SqliteException>(() => provider.GetRequiredService<IKanaSessionOperations>().StartOrResumeAsync(token));
        }

        private static Task<ServiceProvider> CreateAsync(string root, TimeProvider? clock = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, createLexicons: false, configure: services =>
            {
                if (clock is not null) services.AddSingleton(clock);
                services.AddSingleton(new Random(17));
            });
        private static Task SaveAsync(ServiceProvider provider, KanaPlanDto plan) => provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(plan, token);
        private static async Task<long> ScalarAsync(ServiceProvider provider, string sql)
        {
            using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(token);
            return await connection.ExecuteScalarAsync<long>(sql);
        }
        private static string Answer(KanaQuestionDto question)
        {
            var literals = question.Script == KanaScript.Hiragana ? $"{clear} {voiced} {contracted}" : $"{clearKatakana} {voicedKatakana} {contractedKatakana}";
            return literals.Split(' ').Single(text => question.KanaId == $"{question.Script}:{text}");
        }
        private static async Task<KanaFeedbackDto> CompleteAsync(ServiceProvider provider, KanaSessionDto session, Func<KanaQuestionDto, string> answer)
        {
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var reader = provider.GetRequiredService<IKanaSessionReader>();
            var question = await reader.GetCurrentQuestionAsync(session.SessionId, token);
            KanaFeedbackDto? result = null;
            for (var safety = 0; question is not null && safety < 500; safety++)
            {
                result = await operations.SubmitAsync(session.SessionId, question.QuestionOrdinal, answer(question), token);
                question = result.NextQuestion;
            }
            Assert.NotNull(result);
            Assert.True(result.IsCompleted);
            return result;
        }
        /// <summary>在隔离测试中控制UTC与本地日期，不依赖机器时区或实际等待。</summary>
        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; }
            public override DateTimeOffset GetUtcNow() => Now;
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");
        }
    }
}
