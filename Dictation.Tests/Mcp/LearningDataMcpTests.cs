using System.Text.Json;
using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Mistakes;
using Dictation.Core.Review;
using Dictation.Core.Statistics;
using Dictation.Presentation.Mcp;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Dictation.Tests.Mcp
{
    /// <summary>使用真实业务入口和数据库验证学习只读工具的筛选、分页、预测与状态隔离。</summary>
    public sealed class LearningDataMcpTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task Overview_EmptyData_ReturnsLanguageAndHonestCounts(LearningLanguage language)
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var result = Read(await Tools(env).OverviewAsync(language, Token));
            var statistics = result.GetProperty("statistics");
            Assert.Equal(language.ToString(), statistics.GetProperty("language").GetString());
            Assert.Equal(MemorySeedTestEnvironment.Now, statistics.GetProperty("asOfUtc").GetDateTimeOffset());
            Assert.Equal(0, statistics.GetProperty("learnedGroupCount").GetInt64());
            Assert.Equal(0, statistics.GetProperty("retentionTrend").GetArrayLength());
            Assert.True(result.GetProperty("estimatedRetentionIsPrediction").GetBoolean());
        }

        [Fact]
        public async Task MemoryDetails_CompletedAndUntestedForms_PreservesFsrsAndPagesStableKeys()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var groupId = await env.ImportAsync();
            await env.SavePlanAsync("Dictionary");
            await env.FinishAsync();
            var tools = Tools(env);
            var page = Read(await tools.MemoryDetailsAsync(LearningLanguage.Japanese, limit: 1, cancellationToken: Token));
            Assert.Equal(4, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, page.GetProperty("items").GetArrayLength());
            Assert.Equal(MemorySeedTestEnvironment.Now, page.GetProperty("asOfUtc").GetDateTimeOffset());
            var row = page.GetProperty("items")[0];
            Assert.Equal(groupId, row.GetProperty("wordGroupId").GetString());
            var memory = row.GetProperty("memory");
            Assert.Equal("Dictionary", memory.GetProperty("formCode").GetString());
            Assert.True(memory.GetProperty("hasMemory").GetBoolean());
            Assert.Equal(2.3065, memory.GetProperty("stability").GetDouble());
            Assert.Equal(2.118103970459016, memory.GetProperty("difficulty").GetDouble(), precision: 12);
            Assert.Equal(1, memory.GetProperty("estimatedRetention").GetDouble());
            Assert.Equal(1, memory.GetProperty("reviewCount").GetInt32());
            Assert.Equal(0, memory.GetProperty("lapseCount").GetInt32());
            Assert.Equal(MemorySeedTestEnvironment.Now, memory.GetProperty("lastReviewedAtUtc").GetDateTimeOffset());
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(2), memory.GetProperty("dueAtUtc").GetDateTimeOffset());
            Assert.True(page.GetProperty("estimatedRetentionIsPrediction").GetBoolean());

            var filtered = Read(await tools.MemoryDetailsAsync(LearningLanguage.Japanese, groupId, "jmdict:1:1", "Polite", cancellationToken: Token));
            Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
            var untested = filtered.GetProperty("items")[0].GetProperty("memory");
            Assert.False(untested.GetProperty("hasMemory").GetBoolean());
            foreach (var field in new[] { "stability", "difficulty", "estimatedRetention", "lastReviewedAtUtc", "dueAtUtc" })
            {
                Assert.Equal(JsonValueKind.Null, untested.GetProperty(field).ValueKind);
            }
            Assert.Equal(0, Read(await tools.MemoryDetailsAsync(LearningLanguage.English, groupId, cancellationToken: Token)).GetProperty("totalCount").GetInt32());
            Assert.Equal(0, Read(await tools.MemoryDetailsAsync(LearningLanguage.Japanese, offset: int.MaxValue, cancellationToken: Token)).GetProperty("items").GetArrayLength());
            Assert.Equal("Polite", Read(await tools.MemoryDetailsAsync(LearningLanguage.Japanese, offset: 1, limit: 1, cancellationToken: Token))
                .GetProperty("items")[0].GetProperty("memory").GetProperty("formCode").GetString());
        }

        [Fact]
        public async Task WrongAnswers_RepeatedErrors_ReturnsBoundedSummaryAndIndependentHistory()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var groupId = await env.ImportAsync();
            await env.SavePlanAsync("Dictionary");
            var session = await env.Sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
            var question = (await env.Sessions.GetCurrentQuestionAsync(session.SessionId, Token))!;
            var feedback = await env.Sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "错误一"), Token);
            await env.Sessions.SubmitAnswerAsync(new(session.SessionId, feedback.NextQuestion!.QuestionId, "错误二"), Token);
            var tools = Tools(env);
            var page = Read(await tools.WrongAnswersAsync(LearningLanguage.Japanese, groupId, "jmdict:1:1", "Dictionary", limit: 1, cancellationToken: Token));
            Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
            var summary = page.GetProperty("items")[0];
            Assert.Equal(2, summary.GetProperty("errorCount").GetInt32());
            Assert.Equal(groupId, summary.GetProperty("wordGroupId").GetString());
            Assert.False(summary.TryGetProperty("attempts", out _));
            var history = Read(await tools.WrongAnswerHistoryAsync(LearningLanguage.Japanese, groupId, "jmdict:1:1", "Dictionary", limit: 1, offset: 1, cancellationToken: Token));
            Assert.Equal(2, history.GetProperty("totalCount").GetInt32());
            var attempt = history.GetProperty("items")[0];
            Assert.Equal("Learning", attempt.GetProperty("mode").GetString());
            Assert.Equal("たべる", attempt.GetProperty("expectedAnswer").GetString());
            Assert.Contains(attempt.GetProperty("submittedText").GetString(), new[] { "错误一", "错误二" });
            Assert.Equal(0, Read(await tools.WrongAnswersAsync(LearningLanguage.English, cancellationToken: Token)).GetProperty("totalCount").GetInt32());
            Assert.Equal(0, Read(await tools.WrongAnswerHistoryAsync(LearningLanguage.Japanese, "unknown", "jmdict:1:1", "Dictionary", cancellationToken: Token)).GetProperty("totalCount").GetInt32());
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(101, 0)]
        [InlineData(20, -1)]
        public async Task Queries_InvalidPaging_ReturnsControlledError(int limit, int offset)
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var tools = Tools(env);
            Assert.True((await tools.MemoryDetailsAsync(LearningLanguage.Japanese, limit: limit, offset: offset, cancellationToken: Token)).IsError);
            Assert.True((await tools.WrongAnswersAsync(LearningLanguage.Japanese, limit: limit, offset: offset, cancellationToken: Token)).IsError);
            Assert.True((await tools.WrongAnswerHistoryAsync(LearningLanguage.Japanese, "g", "s", "Base", limit, offset, Token)).IsError);
            Assert.True((await tools.PlansAsync(LearningLanguage.Japanese, limit, offset, Token)).IsError);
        }

        [Fact]
        public async Task Queries_InvalidLanguageIdentifiersAndCancellation_RejectsWithoutLeakingDetails()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var tools = Tools(env);
            Assert.True((await tools.OverviewAsync((LearningLanguage)99, Token)).IsError);
            Assert.True((await tools.PlansAsync((LearningLanguage)99, cancellationToken: Token)).IsError);
            Assert.True((await tools.MemoryDetailsAsync((LearningLanguage)99, cancellationToken: Token)).IsError);
            Assert.True((await tools.WrongAnswersAsync((LearningLanguage)99, cancellationToken: Token)).IsError);
            Assert.True((await tools.WrongAnswerHistoryAsync(LearningLanguage.Japanese, "", "s", "Base", cancellationToken: Token)).IsError);
            Assert.True((await tools.MemoryDetailsAsync(LearningLanguage.Japanese, wordGroupId: " ", cancellationToken: Token)).IsError);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.MemoryDetailsAsync(LearningLanguage.Japanese, cancellationToken: new(true)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.OverviewAsync(LearningLanguage.Japanese, new(true)));
            await using var connection = env.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync("DROP TABLE LearningSharedAnswerAttempts;");
            var error = await tools.WrongAnswersAsync(LearningLanguage.Japanese, cancellationToken: Token);
            Assert.True(error.IsError);
            var message = Assert.Single(error.Content.OfType<TextContentBlock>()).Text;
            Assert.Contains("business_error", message);
            Assert.DoesNotContain("LearningSharedAnswerAttempts", message);
            Assert.DoesNotContain("Exception", message);
        }

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task Plans_ConcurrentReads_ReturnDefaultsWithoutWritingAnyBusinessTable(LearningLanguage language)
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await using var connection = env.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            var before = await SnapshotAsync(connection);
            var tools = Tools(env);
            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => tools.PlansAsync(language, cancellationToken: Token)));
            foreach (var result in results)
            {
                var value = Read(result);
                Assert.Equal(language.ToString(), value.GetProperty("language").GetString());
                Assert.Equal(language.ToString(), value.GetProperty("learning").GetProperty("language").GetString());
                Assert.Equal(language.ToString(), value.GetProperty("review").GetProperty("language").GetString());
                Assert.Equal(0, value.GetProperty("totalSelectedGroupCount").GetInt32());
            }
            Read(await tools.OverviewAsync(language, Token));
            Read(await tools.WrongAnswersAsync(language, cancellationToken: Token));
            Read(await tools.MemoryDetailsAsync(language, cancellationToken: Token));
            Assert.Equal(before, await SnapshotAsync(connection));
        }

        private static DictationLearningMcpTools Tools(MemorySeedTestEnvironment env)
        {
            var services = env.Provider;
            return new DictationLearningMcpTools(services.GetRequiredService<ILanguageStatisticsReader>(),
                services.GetRequiredService<IWrongAnswerQuery>(), services.GetRequiredService<IReviewLibraryQuery>(),
                services.GetRequiredService<ILearningPlanReader>(), services.GetRequiredService<IReviewPlanReader>(),
                services.GetRequiredService<TimeProvider>());
        }

        private static JsonElement Read(CallToolResult result)
        {
            Assert.NotEqual(true, result.IsError);
            using var document = JsonDocument.Parse(Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
            return document.RootElement.Clone();
        }

        private static async Task<string> SnapshotAsync(System.Data.Common.DbConnection connection)
        {
            var tables = await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");
            var contents = new List<string>();
            foreach (var table in tables)
            {
                contents.Add(table + JsonSerializer.Serialize(await connection.QueryAsync("SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\" ORDER BY rowid;")));
            }
            return string.Join("\n", contents);
        }
    }
}
