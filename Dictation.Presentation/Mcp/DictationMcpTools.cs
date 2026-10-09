using System.ComponentModel;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Dictation.Presentation.Mcp
{
    /// <summary>公开源语言查询与待确认导入能力，所有业务都委派到原Core接口。</summary>
    /// <param name="search">正向离线查词能力。</param>
    /// <param name="preparation">只读候选准备能力。</param>
    /// <param name="staging">整组暂存能力，不能确认去向。</param>
    /// <param name="batches">只读批次回查能力。</param>
    /// <param name="status">监听状态快照能力。</param>
    [McpServerToolType]
    internal sealed class DictationMcpTools(ILexiconSearch search, ITextImportPreparation preparation,
        IImportStaging staging, IImportBatchReader batches, IMcpStatusReader status)
    {
        /// <summary>说明支持语言、导入确认流程与禁止能力。</summary>
        [McpServerTool(Name = "dictation_capabilities", ReadOnly = true, OpenWorld = false)]
        [Description("查询能力与流程：只支持Japanese／English源语言检索，整组导入先进入收件箱，由用户决定学习、复习或丢弃。不能直接确认、删除或修改进度。")]
        public CallToolResult Capabilities() => McpToolResultMapper.Success(new { Languages = new[] { "Japanese", "English" }, UserConfirmationRequired = true,
            WholeWordGroup = true, ChineseReverseLookup = false, DirectConfirmation = false, DirectProgressWrite = false });

        /// <summary>按源语言正向查询离线中文义项及稳定选择标识。</summary>
        /// <param name="language">Japanese或English。</param>
        /// <param name="query">源语言词形，不接受中文反向查询。</param>
        /// <param name="limit">一页结果上限。</param>
        /// <param name="offset">分页起始位置。</param>
        /// <param name="cancellationToken">取消请求。</param>
        [McpServerTool(Name = "dictation_lexicon_search", ReadOnly = true, OpenWorld = false)]
        [Description("离线正向查词，返回中文释义、词性、词形及sourceSenseId；请用返回的稳定义项标识准备导入，不凭空生成义项ID。")]
        public Task<CallToolResult> SearchAsync(LearningLanguage language, string query, int limit = 20, int offset = 0, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () => await search.SearchAsync(new(language, query, limit, offset), cancellationToken));

        /// <summary>准备每行一词的候选，只读且保留未知／歧义项目。</summary>
        /// <param name="language">源语言。</param>
        /// <param name="text">每行一个源语言词形。</param>
        /// <param name="cancellationToken">取消准备。</param>
        [McpServerTool(Name = "dictation_import_prepare", ReadOnly = true, OpenWorld = false)]
        [Description("准备每行一词的日语或英语词表，返回精确候选和未知项目；不自动选义项，不写入收件箱。")]
        public Task<CallToolResult> PrepareAsync(LearningLanguage language, string text, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () => await preparation.PrepareAsync(language, text, cancellationToken));

        /// <summary>将明确选择的义项整组暂存，不能决定其学习／复习去向。</summary>
        /// <param name="language">本批固定源语言。</param>
        /// <param name="selections">至多100个明确选择，包含源词形及查询得到的义项ID。</param>
        /// <param name="cancellationToken">取消暂存。</param>
        [McpServerTool(Name = "dictation_import_stage", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("把明确选择的sourceSenseId整组暂存到用户收件箱，每批1至100项；返回新增／重复和batchId。必须由用户在应用内确认，不能直接进入学习库或复习库。")]
        public Task<CallToolResult> StageAsync(LearningLanguage language, ImportSelectionDto[] selections, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () =>
            {
                if (selections is null || selections.Length is < 1 or > 100)
                {
                    throw new ArgumentException("暂存每批必须有1至100项。");
                }
                return await staging.StageAsync(new(language, ImportSourceKind.Mcp, selections), cancellationToken);
            });

        /// <summary>按标识查询待处理或已处理批次，不改变用户决定。</summary>
        /// <param name="batchId">暂存返回的稳定批次ID。</param>
        /// <param name="cancellationToken">取消查询。</param>
        [McpServerTool(Name = "dictation_import_status", ReadOnly = true, OpenWorld = false)]
        [Description("回查指定batchId的状态，已确认或丢弃的批次仍可查询；不会替用户确认。")]
        public Task<CallToolResult> StatusAsync(string batchId, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () => await batches.GetBatchAsync(batchId, cancellationToken));

        /// <summary>仅返回公开监听状态和确认流程，不读取教材、用户配置或令牌。</summary>
        [McpServerTool(Name = "dictation_diagnostics", ReadOnly = true, OpenWorld = false)]
        [Description("读取应用版本、本机监听状态和导入确认说明，不提供本机路径、配置文件、数据库或凭据。")]
        public CallToolResult Diagnostics() => McpToolResultMapper.Success(new { Version = typeof(DictationMcpTools).Assembly.GetName().Version?.ToString(),
            Status = status.GetStatus(), UserConfirmationRequired = true });

        /// <summary>将业务结果转换为工具协议，取消保持原样，内部错误不泄露实现细节。</summary>
        /// <param name="action">调用原Core能力的操作。</param>
        private static async Task<CallToolResult> ExecuteAsync(Func<Task<object>> action)
        {
            try
            {
                return McpToolResultMapper.Success(await action());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                return McpToolResultMapper.Error("参数无效，请使用对应语言的有效词形、义项和数量。");
            }
            catch (Exception)
            {
                return McpToolResultMapper.Error("操作失败（business_error），请检查词库或批次是否存在。用户进度不会由此工具修改。");
            }
        }
    }
}
