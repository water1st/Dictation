using System.Text.Json;
using Dictation.Presentation.Mcp;

namespace Dictation.Tests.Mcp
{
    /// <summary>验证可空类型的兼容表达保留嵌套约束、组合条件及字面量数据。</summary>
    public sealed class McpInputSchemaNormalizerTests
    {
        [Fact]
        public void Normalize_NestedNullableTypes_PreservesArrayConstraintsAndLiteralData()
        {
            using var document = JsonDocument.Parse("""
                {
                  "type":"object",
                  "properties":{
                    "wordGroupId":{"type":["string","null"],"minLength":1,"default":null,"description":"可选词组"},
                    "selections":{"type":"array","items":{"type":"object","properties":{
                      "chineseMeaningOverride":{"type":["array","null"],"items":{"type":"string"},"minItems":1,"default":null}
                    },"required":["chineseMeaningOverride"]}},
                    "payload":{"type":"object","default":{"type":["string","null"]}}
                  },
                  "required":["selections"],
                  "additionalProperties":false
                }
                """);
            var result = McpInputSchemaNormalizer.Normalize(document.RootElement);
            var properties = result.GetProperty("properties");
            var identifier = properties.GetProperty("wordGroupId");
            Assert.False(identifier.TryGetProperty("type", out _));
            Assert.Equal(new[] { "string", "null" }, identifier.GetProperty("anyOf").EnumerateArray().Select(branch => branch.GetProperty("type").GetString()));
            Assert.Equal(1, identifier.GetProperty("minLength").GetInt32());
            Assert.Equal("可选词组", identifier.GetProperty("description").GetString());
            Assert.Equal(JsonValueKind.Null, identifier.GetProperty("default").ValueKind);
            var selection = properties.GetProperty("selections");
            Assert.Equal("array", selection.GetProperty("type").GetString());
            var meanings = selection.GetProperty("items").GetProperty("properties").GetProperty("chineseMeaningOverride");
            Assert.False(meanings.TryGetProperty("type", out _));
            Assert.Equal(new[] { "array", "null" }, meanings.GetProperty("anyOf").EnumerateArray().Select(branch => branch.GetProperty("type").GetString()));
            Assert.Equal("string", meanings.GetProperty("items").GetProperty("type").GetString());
            Assert.Equal(1, meanings.GetProperty("minItems").GetInt32());
            Assert.Equal("selections", Assert.Single(result.GetProperty("required").EnumerateArray()).GetString());
            Assert.False(result.GetProperty("additionalProperties").GetBoolean());
            Assert.True(JsonElement.DeepEquals(document.RootElement.GetProperty("properties").GetProperty("payload").GetProperty("default"),
                properties.GetProperty("payload").GetProperty("default")));
        }

        [Fact]
        public void Normalize_ExistingAnyOfAndAllOf_PreservesBothConstraints()
        {
            using var document = JsonDocument.Parse("""
                {"type":"object","properties":{"value":{
                  "type":["string","null"],
                  "anyOf":[{"const":null},{"enum":["first","second"]}],
                  "allOf":[{"description":"原有约束"}]
                }}}
                """);
            var result = McpInputSchemaNormalizer.Normalize(document.RootElement).GetProperty("properties").GetProperty("value");
            Assert.False(result.TryGetProperty("type", out _));
            Assert.Equal(2, result.GetProperty("anyOf").GetArrayLength());
            Assert.Equal("first", result.GetProperty("anyOf")[1].GetProperty("enum")[0].GetString());
            Assert.Equal(2, result.GetProperty("allOf").GetArrayLength());
            Assert.Equal("原有约束", result.GetProperty("allOf")[0].GetProperty("description").GetString());
            Assert.Equal(new[] { "string", "null" }, result.GetProperty("allOf")[1].GetProperty("anyOf")
                .EnumerateArray().Select(branch => branch.GetProperty("type").GetString()));
        }

        [Fact]
        public void Normalize_SingleTypeArray_UsesStandardTypeString()
        {
            using var document = JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":["string"],"enum":["first","second"]}}} """);
            var result = McpInputSchemaNormalizer.Normalize(document.RootElement).GetProperty("properties").GetProperty("query");
            Assert.Equal(JsonValueKind.String, result.GetProperty("type").ValueKind);
            Assert.Equal("string", result.GetProperty("type").GetString());
            Assert.Equal(2, result.GetProperty("enum").GetArrayLength());
        }
    }
}
