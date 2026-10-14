namespace Dictation.Presentation.Mcp
{
    /// <summary>向设置页面和诊断工具公开监听状态，不公开异常堆栈或本机文件路径。</summary>
    /// <param name="State">真实监听状态。</param>
    /// <param name="Endpoint">固定本机端点。</param>
    /// <param name="ErrorMessage">可供用户处理的简明错误。</param>
    public sealed record McpStatusDto(McpRunState State, Uri Endpoint, string? ErrorMessage = null);
}
