using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Dictation.Presentation.Mcp
{
    /// <summary>在协议边界规范输入类型表达，保留工具参数及业务数据的原始语义。</summary>
    internal static class McpInputSchemaNormalizer
    {
        private static readonly AIJsonSchemaTransformOptions TransformOptions = new()
        {
            TransformSchemaNode = (_, schema) => NormalizeType(schema)
        };

        /// <summary>由现有库遍历真正的 Schema 节点，避免修改 default、enum 中的用户字面量。</summary>
        /// <param name="schema">SDK 生成的完整输入 Schema。</param>
        /// <returns>类型表达兼容化后的独立 Schema。</returns>
        internal static JsonElement Normalize(JsonElement schema)
        {
            return AIJsonUtilities.TransformSchema(schema, TransformOptions);
        }

        private static JsonNode NormalizeType(JsonNode schema)
        {
            if (schema is not JsonObject schemaObject || schemaObject["type"] is not JsonArray types)
            {
                return schema;
            }

            if (types.Count == 1)
            {
                schemaObject["type"] = types[0]!.DeepClone();
                return schema;
            }

            var alternatives = new JsonArray();
            foreach (var type in types)
            {
                alternatives.Add(new JsonObject { ["type"] = type!.DeepClone() });
            }
            schemaObject.Remove("type");
            if (!schemaObject.ContainsKey("anyOf"))
            {
                schemaObject["anyOf"] = alternatives;
            }
            else
            {
                // 原有 anyOf 与类型限制需要同时成立，不能用新分支覆盖已有条件。
                var conjunction = schemaObject["allOf"] as JsonArray;
                if (conjunction is null)
                {
                    conjunction = new JsonArray();
                    schemaObject["allOf"] = conjunction;
                }
                conjunction.Add(new JsonObject { ["anyOf"] = alternatives });
            }
            return schema;
        }
    }
}
