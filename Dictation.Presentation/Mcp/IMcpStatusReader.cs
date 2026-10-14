namespace Dictation.Presentation.Mcp
{
    /// <summary>为界面和诊断工具提供只读监听状态。</summary>
    public interface IMcpStatusReader
    {
        /// <summary>获取当前状态快照，不开启服务。</summary>
        /// <returns>当前状态、端点和简明错误。</returns>
        McpStatusDto GetStatus();
    }
}
