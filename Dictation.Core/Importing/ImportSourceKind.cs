namespace Dictation.Core.Importing
{
    /// <summary>
    /// 指示导入批次的来源渠道。
    /// </summary>
    public enum ImportSourceKind
    {
        /// <summary>由用户在界面中手动选择。</summary>
        Manual,

        /// <summary>来自文本解析流程。</summary>
        Text,

        /// <summary>来自本机 MCP 调用。</summary>
        Mcp
    }
}
