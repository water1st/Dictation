using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace Dictation.Presentation.Mcp
{
    /// <summary>将工具结果转换为官方SDK内容，不执行业务或数据库操作。</summary>
    internal static class McpToolResultMapper
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

        /// <summary>输出相同的结构化JSON及文本，兼容只读取文本内容的客户端。</summary>
        /// <param name="value">Core返回的业务结果或公开诊断。</param>
        internal static CallToolResult Success(object value)
        {
            var json = JsonSerializer.SerializeToElement(value, Options);
            return new() { Content = [new TextContentBlock { Text = json.GetRawText() }], StructuredContent = json };
        }

        /// <summary>只输出安全错误说明，不暴露堆栈、SQL或本机路径。</summary>
        /// <param name="message">简明业务错误。</param>
        internal static CallToolResult Error(string message) => new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
    }
}
