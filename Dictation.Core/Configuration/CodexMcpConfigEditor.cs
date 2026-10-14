using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Dictation.Core.Configuration
{
    /// <summary>通过Tomlyn语法树仅编辑本应用项，保留其它表、注释和字符串表示，不进行IO。</summary>
    internal static class CodexMcpConfigEditor
    {
        private static readonly string[] ServerPath = ["mcp_servers", "dictation"];

        /// <summary>验证整份文档并读取属于本应用的本机端点，名称被其它服务使用时拒绝操作。</summary>
        /// <param name="source">完整TOML文档。</param>
        /// <returns>当前本应用端点，未注册时为空。</returns>
        internal static Uri? ReadEndpoint(string source)
        {
            Parse(source);
            var model = TomlSerializer.Deserialize<TomlTable>(source)!;
            if (!model.TryGetValue("mcp_servers", out var serversValue)) return null;
            if (serversValue is not TomlTable servers) throw Conflict();
            if (!servers.TryGetValue("dictation", out var serverValue)) return null;
            if (serverValue is not TomlTable server || server.ContainsKey("command") || server.ContainsKey("args")
                || !server.TryGetValue("url", out var url) || url is not string address
                || !Uri.TryCreate(address, UriKind.Absolute, out var endpoint) || !IsLocalEndpoint(endpoint))
                throw Conflict();
            return endpoint;
        }

        /// <summary>新增本应用注册，同名不同端点不覆盖；已匹配时返回原文。</summary>
        /// <param name="source">经过读取的原文。</param>
        /// <param name="endpoint">已校验的本机端点。</param>
        /// <returns>仅新增本应用项后的文档。</returns>
        internal static string Register(string source, Uri endpoint)
        {
            var existing = ReadEndpoint(source);
            if (existing is not null)
            {
                if (existing != endpoint) throw Conflict();
                return source;
            }
            var document = Parse(source);
            var inline = document.KeyValues.FirstOrDefault(item => PathOf(item.Key).SequenceEqual(["mcp_servers"]))?.Value as InlineTableSyntax;
            if (inline is not null)
            {
                // TOML内联表是封闭的，不能在文件末尾另开子表；直接组合库提供的语法节点。
                var url = new KeyValueSyntax("url", new StringValueSyntax(endpoint.AbsoluteUri)) { EndOfLineToken = null };
                var entry = new KeyValueSyntax("dictation", new InlineTableSyntax(url)) { EndOfLineToken = null };
                if (inline.Items.ChildrenCount > 0 && inline.Items.GetChild(inline.Items.ChildrenCount - 1) is { Comma: null } last)
                    last.Comma = SyntaxFactory.Token(TokenKind.Comma).AddTrailingWhitespace();
                inline.Items.Add(new InlineTableItemSyntax(entry));
                return document.ToString();
            }
            var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var separator = source.Length == 0 || source.EndsWith('\n') ? string.Empty : newline;
            return source + separator + "[mcp_servers.dictation]" + newline + "url = \"" + endpoint.AbsoluteUri + "\"" + newline;
        }

        /// <summary>删除本应用节点，保留兄弟节点及附着于后续表的注释。</summary>
        /// <param name="source">完整原文。</param>
        /// <returns>注销后的文档；未注册时返回原文。</returns>
        internal static string Unregister(string source)
        {
            if (ReadEndpoint(source) is null) return source;
            var document = Parse(source);
            RemoveValues(document.KeyValues, []);
            foreach (var table in document.Tables.ToArray())
            {
                var path = PathOf(table.Name);
                if (BelongsToServer(path))
                {
                    // 解析器把后续表前的独立注释附在上一表末行的换行符上，删除前转移给下一表。
                    var following = document.Tables.SkipWhile(candidate => !ReferenceEquals(candidate, table)).Skip(1).FirstOrDefault();
                    var trailing = table.Items.LastOrDefault()?.EndOfLineToken?.TrailingTrivia;
                    if (following is not null && trailing is not null)
                    {
                        foreach (var trivia in trailing) following.OpenBracket!.AddLeadingTrivia(trivia);
                    }
                    document.Tables.RemoveChild(table);
                }
                else RemoveValues(table.Items, path);
            }
            return document.ToString();
        }

        /// <summary>限制仅本机固定HTTP协议，不接受用户信息、查询或片段。</summary>
        /// <param name="endpoint">待注册地址。</param>
        /// <returns>是否是本应用能够监听的地址。</returns>
        internal static bool IsLocalEndpoint(Uri endpoint) => endpoint.IsAbsoluteUri && endpoint.Scheme == Uri.UriSchemeHttp
            && endpoint.Host == "127.0.0.1" && endpoint.Port is >= 1024 and <= 65535 && endpoint.AbsolutePath == "/mcp"
            && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0;

        /// <summary>委派语法及重复键校验，不向界面泄露客户端配置或解析上下文。</summary>
        private static DocumentSyntax Parse(string source)
        {
            var document = SyntaxParser.Parse(source);
            if (document.HasErrors) throw new SettingsConfigurationException("Codex配置语法无效，请先修正后重试。原文件未修改。");
            return document;
        }

        /// <summary>在当前表中按库解析出的键路径移除目标，内联表递归但不扩展业务对象。</summary>
        private static void RemoveValues(SyntaxList<KeyValueSyntax> values, string[] prefix)
        {
            foreach (var item in values.ToArray())
            {
                var path = prefix.Concat(PathOf(item.Key)).ToArray();
                if (BelongsToServer(path)) values.RemoveChild(item);
                else if (item.Value is InlineTableSyntax inline) RemoveInline(inline, path);
            }
        }

        /// <summary>删除内联表目标后修正库节点的末尾逗号，不重新格式化其它值。</summary>
        private static void RemoveInline(InlineTableSyntax inline, string[] prefix)
        {
            foreach (var item in inline.Items.ToArray())
            {
                var path = prefix.Concat(PathOf(item.KeyValue?.Key)).ToArray();
                if (BelongsToServer(path)) inline.Items.RemoveChild(item);
                else if (item.KeyValue?.Value is InlineTableSyntax child) RemoveInline(child, path);
            }
            if (inline.Items.ChildrenCount > 0 && inline.Items.GetChild(inline.Items.ChildrenCount - 1) is { } last) last.Comma = null;
        }

        /// <summary>读取语法树已经解码的裸键或引号键，不自行拆解TOML字符串。</summary>
        private static string[] PathOf(KeySyntax? key) => key is null ? []
            : new[] { KeyText(key.Key) }.Concat(key.DotKeys.Select(item => KeyText(item.Key))).ToArray();

        /// <summary>从库的类型化键节点读取真实键值。</summary>
        private static string KeyText(BareKeyOrStringValueSyntax? key) => key switch
        {
            BareKeySyntax bare => bare.Key!.Text!,
            StringValueSyntax quoted => (string)quoted.Value!,
            _ => throw Conflict()
        };

        /// <summary>判断语法节点是否属于本应用项或其子表。</summary>
        private static bool BelongsToServer(string[] path) => path.Length >= 2 && path.Take(2).SequenceEqual(ServerPath);

        /// <summary>生成不包含用户配置内容的冲突说明。</summary>
        private static SettingsConfigurationException Conflict() => new("Codex中dictation名称已有不同配置。请检查或先注销旧端点，不会覆盖现有服务器。");
    }
}
