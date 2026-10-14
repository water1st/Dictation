namespace Dictation.Presentation.Mcp
{
    /// <summary>表示本机MCP监听器的真实生命周期状态。</summary>
    public enum McpRunState
    {
        Disabled,
        Starting,
        Running,
        Failed,
        Stopping
    }
}
