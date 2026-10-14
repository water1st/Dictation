using Dictation.Core.Practice;
using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>为记忆初始化测试组合真实 DI、SQLite、词库和可重现时钟。</summary>
    internal sealed class MemorySeedTestEnvironment : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        private ServiceProvider provider = null!;
        internal static DateTimeOffset Now { get; } = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        internal SqliteConnectionFactory Connections => provider.GetRequiredService<SqliteConnectionFactory>();
        internal LearningSessionService Sessions => provider.GetRequiredService<LearningSessionService>();
        internal ServiceProvider Provider => provider;

        internal static async Task<MemorySeedTestEnvironment> CreateAsync()
        {
            var environment = new MemorySeedTestEnvironment();
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                services.AddDictationDataAccess(environment.directory.Path).AddDictationCore();
                services.AddSingleton<TimeProvider>(new FixedClock());
                services.AddSingleton(new Random(100));
                environment.provider = services.BuildServiceProvider();
                var paths = environment.provider.GetRequiredService<ApplicationPaths>();
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                await environment.provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
                return environment;
            }
            catch
            {
                environment.Dispose();
                throw;
            }
        }

        internal async Task<string> ImportAsync(string word = "食べる", string sense = "jmdict:1:1",
            ImportDecision decision = ImportDecision.Learning)
        {
            var stage = await provider.GetRequiredService<IImportStaging>().StageAsync(
                new StageImportBatchRequestDto(LearningLanguage.Japanese, ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto(word, sense) }), TestContext.Current.CancellationToken);
            var item = Assert.Single(stage.Batch.Items);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(
                new DecideImportBatchRequestDto(stage.Batch.BatchId, new[] { item.ItemId }, decision), TestContext.Current.CancellationToken);
            return item.WordGroupId!;
        }

        internal Task<LearningPlanDto> SavePlanAsync(params string[] forms) => provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(
            new SaveLearningPlanRequestDto(LearningLanguage.Japanese, 20, 20, LearningSelectionMode.Sequential,
                EnglishFormLabelMode.GrammarName, null, forms,
                new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }), TestContext.Current.CancellationToken);

        internal async Task<string> CorrectAnswerAsync(PracticeQuestionDto question)
        {
            await using var connection = Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await connection.ExecuteScalarAsync<string>("""
                SELECT Forms.Surface FROM WordGroupForms AS Forms
                INNER JOIN LearningQuestions AS Questions ON Questions.MemberId = Forms.MemberId
                WHERE Questions.QuestionId = @QuestionId AND Forms.FormCode = Questions.FormCode
                  AND Forms.ScriptCode = Questions.OutputMode
                ORDER BY Forms.IsPrimary DESC, Forms.FormIndex LIMIT 1;
                """, new { question.QuestionId }) ?? throw new InvalidOperationException("测试题目缺少正确词形。");
        }

        internal async Task FinishAsync(string? errorForm = null)
        {
            var session = await Sessions.StartOrResumeAsync(new StartLearningSessionRequestDto(LearningLanguage.Japanese), TestContext.Current.CancellationToken);
            var question = await Sessions.GetCurrentQuestionAsync(session.SessionId, TestContext.Current.CancellationToken);
            var madeError = false;
            for (var index = 0; question is not null && index < 100; index++)
            {
                var injectError = !madeError && question.FormCode == errorForm;
                var answer = injectError ? "故意写错" : await CorrectAnswerAsync(question);
                madeError |= injectError;
                var result = await Sessions.SubmitAnswerAsync(new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId, answer), TestContext.Current.CancellationToken);
                question = result.NextQuestion;
            }
            Assert.Null(question);
            if (errorForm is not null) Assert.True(madeError);
        }

        internal async Task<IReadOnlyList<dynamic>> MemoriesAsync(string groupId)
        {
            await using var connection = Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return (await connection.QueryAsync("SELECT * FROM ReviewFormMemories WHERE WordGroupId = @GroupId ORDER BY MemberId, FormCode;",
                new { GroupId = groupId })).ToArray();
        }

        internal async Task<long> MemberAsync(string groupId)
        {
            await using var connection = Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await connection.ExecuteScalarAsync<long>("SELECT MemberId FROM WordGroupMembers WHERE WordGroupId = @GroupId ORDER BY MemberId LIMIT 1;", new { GroupId = groupId });
        }

        public void Dispose()
        {
            provider?.Dispose();
            directory.Dispose();
        }

        /// <summary>冻结 UTC 时钟，避免目标日期和当地日期随实际测试运行变化。</summary>
        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
