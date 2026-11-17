using System.ComponentModel;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Mistakes;
using Dictation.Core.Review;
using Dictation.Core.Statistics;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Dictation.Presentation.Mcp
{
    /// <summary>将已有学习数据只读能力适配为受限的 MCP 查询，不持有写入服务。</summary>
    [McpServerToolType]
    internal sealed class DictationLearningMcpTools
    {
        private readonly ILanguageStatisticsReader statistics;
        private readonly IWrongAnswerQuery mistakes;
        private readonly IReviewLibraryQuery library;
        private readonly ILearningPlanReader learningPlan;
        private readonly IReviewPlanReader reviewPlan;
        private readonly TimeProvider clock;

        public DictationLearningMcpTools(ILanguageStatisticsReader statistics, IWrongAnswerQuery mistakes,
            IReviewLibraryQuery library, ILearningPlanReader learningPlan, IReviewPlanReader reviewPlan, TimeProvider clock)
        {
            this.statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            this.mistakes = mistakes ?? throw new ArgumentNullException(nameof(mistakes));
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            this.learningPlan = learningPlan ?? throw new ArgumentNullException(nameof(learningPlan));
            this.reviewPlan = reviewPlan ?? throw new ArgumentNullException(nameof(reviewPlan));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>读取已有统计，保持组计数及 FSRS 预测口径。</summary>
        [McpServerTool(Name = "dictation_learning_overview", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("读取指定 Japanese/English 语言的学习总览。计数单位为词组，保持率为概率预测而非实测值，statistics.asOfUtc 是预测时间；不修改进度。")]
        public Task<CallToolResult> OverviewAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(language, cancellationToken, async () =>
                new McpLearningOverviewDto(await statistics.LoadAsync(language, cancellationToken)));
        }

        /// <summary>按稳定标识筛选累计错词摘要；原始历史由独立分页工具读取。</summary>
        [McpServerTool(Name = "dictation_wrong_answers", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("按 Japanese/English、wordGroupId/sourceSenseId/formCode 精确筛选累计错词摘要。errorCount 为历史错误次数，不代表尚未纠正；limit 为1至100，offset非负。历史请用 dictation_wrong_answer_history，不启动重练。")]
        public Task<CallToolResult> WrongAnswersAsync(LearningLanguage language, string? wordGroupId = null,
            string? sourceSenseId = null, string? formCode = null, int limit = 20, int offset = 0,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(language, cancellationToken, async () =>
            {
                ValidatePage(limit, offset);
                ValidateFilters(wordGroupId, sourceSenseId, formCode);
                var items = (await mistakes.LoadAsync(language, cancellationToken))
                    .Where(item => Matches(item.WordGroupId, wordGroupId) && Matches(item.SourceSenseId, sourceSenseId)
                        && Matches(item.FormCode, formCode))
                    .Select(item => new McpWrongAnswerDto(item.WordGroupId, item.SourceSenseId, item.FormCode,
                        item.Headword, item.FormLabel, item.Meanings, item.Placement, item.DueAtUtc,
                        item.Attempts.Count, item.Attempts.MaxBy(attempt => attempt.AnsweredAtUtc)?.AnsweredAtUtc))
                    .OrderByDescending(item => item.ErrorCount).ThenByDescending(item => item.LastAnsweredAtUtc)
                    .ThenBy(item => item.WordGroupId, StringComparer.Ordinal)
                    .ThenBy(item => item.SourceSenseId, StringComparer.Ordinal)
                    .ThenBy(item => item.FormCode, StringComparer.Ordinal).ToArray();
                return Page(language, clock.GetUtcNow(), items, limit, offset);
            });
        }

        /// <summary>读取单个成员词形的历史错误，避免将全部历史嵌入列表。</summary>
        [McpServerTool(Name = "dictation_wrong_answer_history", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("读取指定语言和 wordGroupId/sourceSenseId/formCode 的原始错误历史，保留 Learning/Review 来源、题型、实际与标准答案。按时间倒序分页，limit为1至100，offset非负；未知标识返回空页。")]
        public Task<CallToolResult> WrongAnswerHistoryAsync(LearningLanguage language, string wordGroupId,
            string sourceSenseId, string formCode, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(language, cancellationToken, async () =>
            {
                ValidatePage(limit, offset);
                ArgumentException.ThrowIfNullOrWhiteSpace(wordGroupId);
                ArgumentException.ThrowIfNullOrWhiteSpace(sourceSenseId);
                ArgumentException.ThrowIfNullOrWhiteSpace(formCode);
                var items = (await mistakes.LoadAsync(language, cancellationToken))
                    .Where(item => Matches(item.WordGroupId, wordGroupId) && Matches(item.SourceSenseId, sourceSenseId)
                        && Matches(item.FormCode, formCode)).SelectMany(item => item.Attempts)
                    .OrderByDescending(item => item.AnsweredAtUtc).ThenBy(item => item.Mode, StringComparer.Ordinal)
                    .ThenBy(item => item.SubmittedText, StringComparer.Ordinal).ToArray();
                return Page(language, clock.GetUtcNow(), items, limit, offset);
            });
        }

        /// <summary>按独立成员词形展开只读复习库，沿用业务层的预测时间与空记忆语义。</summary>
        [McpServerTool(Name = "dictation_memory_details", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("读取 Japanese/English 复习词形的 FSRS 细节，支持 wordGroupId/sourceSenseId/formCode 精确筛选。totalCount以成员词形为单位；稳定度单位为天，estimatedRetention为asOfUtc时的概率预测，hasMemory=false时相关值为null。limit为1至100，offset非负。")]
        public Task<CallToolResult> MemoryDetailsAsync(LearningLanguage language, string? wordGroupId = null,
            string? sourceSenseId = null, string? formCode = null, int limit = 20, int offset = 0,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(language, cancellationToken, async () =>
            {
                ValidatePage(limit, offset);
                ValidateFilters(wordGroupId, sourceSenseId, formCode);
                var snapshot = await library.LoadAsync(language, cancellationToken);
                var items = snapshot.Groups.Where(group => Matches(group.WordGroup.WordGroupId, wordGroupId))
                    .SelectMany(group => group.Forms.Where(form => Matches(form.SourceSenseId, sourceSenseId)
                        && Matches(form.FormCode, formCode)).Select(form => new McpMemoryDetailDto(group.WordGroup.WordGroupId, form)))
                    .OrderBy(item => item.WordGroupId, StringComparer.Ordinal)
                    .ThenBy(item => item.Memory.SourceSenseId, StringComparer.Ordinal)
                    .ThenBy(item => item.Memory.FormCode, StringComparer.Ordinal).ToArray();
                return Page(language, snapshot.AsOfUtc, items, limit, offset, true);
            });
        }

        /// <summary>返回当前生效计划，将可增长的复习选择集合单独分页。</summary>
        [McpServerTool(Name = "dictation_plans", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("读取指定 Japanese/English 的当前学习与复习计划，不保存默认值。review.wordGroupIds按稳定ID分页，totalSelectedGroupCount表示全部已选词组数；limit为1至100，offset非负，其余计划字段完整返回。")]
        public Task<CallToolResult> PlansAsync(LearningLanguage language, int limit = 20, int offset = 0,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(language, cancellationToken, async () =>
            {
                ValidatePage(limit, offset);
                var learning = await learningPlan.LoadAsync(language, cancellationToken);
                var review = await reviewPlan.LoadAsync(language, cancellationToken);
                var ids = review.WordGroupIds.Order(StringComparer.Ordinal).ToArray();
                return new McpPlansDto(language, clock.GetUtcNow(), learning,
                    review with { WordGroupIds = ids.Skip(offset).Take(limit).ToArray() }, ids.Length, limit, offset);
            });
        }

        private static McpReadPageDto<T> Page<T>(LearningLanguage language, DateTimeOffset asOfUtc,
            T[] items, int limit, int offset, bool isPrediction = false)
        {
            return new McpReadPageDto<T>(language, asOfUtc, items.Length, limit, offset,
                items.Skip(offset).Take(limit).ToArray(), isPrediction);
        }

        private static bool Matches(string value, string? filter)
        {
            return filter is null || string.Equals(value, filter, StringComparison.Ordinal);
        }

        private static void ValidatePage(int limit, int offset)
        {
            if (limit is < 1 or > 100 || offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
        }

        private static void ValidateFilters(params string?[] filters)
        {
            foreach (var filter in filters)
            {
                if (filter is not null)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(filter);
                }
            }
        }

        private static async Task<CallToolResult> ExecuteAsync(LearningLanguage language,
            CancellationToken cancellationToken, Func<Task<object>> action)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
                {
                    throw new ArgumentOutOfRangeException(nameof(language));
                }
                var result = await action();
                cancellationToken.ThrowIfCancellationRequested();
                return McpToolResultMapper.Success(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                return McpToolResultMapper.Error("invalid_parameters：language须为Japanese或English，limit须为1至100，offset须非负，标识不可为空白。");
            }
            catch (Exception)
            {
                return McpToolResultMapper.Error("business_error：学习数据读取失败，请在应用中检查数据状态后重试。");
            }
        }
    }
}
