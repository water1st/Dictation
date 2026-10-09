using System.Text;

namespace Dictation.Core.Configuration
{
    /// <summary>编排用户主动触发的Codex注册，仅维护本应用项，不执行CLI或启动客户端。</summary>
    public sealed class CodexMcpRegistrationService
    {
        private static readonly UTF8Encoding Utf8 = new(false, true);
        /// <summary>只读查询本应用注册，不创建配置文件。</summary>
        /// <param name="configPath">客户端配置的完整路径。</param>
        /// <param name="cancellationToken">取消文件读取。</param>
        /// <returns>当前注册状态。</returns>
        public async Task<CodexMcpRegistrationResultDto> ReadAsync(string configPath, CancellationToken cancellationToken = default)
        {
            var bytes = await ReadFileAsync(configPath, cancellationToken);
            var endpoint = CodexMcpConfigEditor.ReadEndpoint(Decode(bytes));
            return new(false, endpoint, endpoint is null ? "尚未注册。" : "已注册；修改端口后请先注销旧端点，再注册新端点。");
        }

        /// <summary>验证端点并注册，仅原子更新本应用项，冲突时不覆盖。</summary>
        /// <param name="configPath">客户端配置的完整路径。</param>
        /// <param name="endpoint">本机HTTP端点。</param>
        /// <param name="cancellationToken">取消写入。</param>
        /// <returns>是否修改及注册状态。</returns>
        public Task<CodexMcpRegistrationResultDto> RegisterAsync(string configPath, Uri endpoint, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            if (!CodexMcpConfigEditor.IsLocalEndpoint(endpoint)) throw new ArgumentException("注册地址必须为有效的127.0.0.1本机HTTP端点。", nameof(endpoint));
            return UpdateAsync(configPath, source => CodexMcpConfigEditor.Register(source, endpoint), cancellationToken);
        }

        /// <summary>仅注销本应用的本机端点，不移除其它服务器。</summary>
        /// <param name="configPath">客户端配置的完整路径。</param>
        /// <param name="cancellationToken">取消写入。</param>
        /// <returns>是否修改及注销状态。</returns>
        public Task<CodexMcpRegistrationResultDto> UnregisterAsync(string configPath, CancellationToken cancellationToken = default) =>
            UpdateAsync(configPath, CodexMcpConfigEditor.Unregister, cancellationToken);

        /// <summary>读入快照后委派语法编辑及带并发保护的原子替换，不改其它客户端配置。</summary>
        private static async Task<CodexMcpRegistrationResultDto> UpdateAsync(string path, Func<string, string> edit, CancellationToken cancellationToken)
        {
            var original = await ReadFileAsync(path, cancellationToken);
            var source = Decode(original);
            var updated = edit(source);
            var endpoint = CodexMcpConfigEditor.ReadEndpoint(updated);
            if (source == updated) return new(false, endpoint, endpoint is null ? "尚未注册。" : "已经注册，无需修改。");
            var body = Utf8.GetBytes(updated);
            var bytes = HasBom(original) ? Encoding.UTF8.GetPreamble().Concat(body).ToArray() : body;
            await CodexConfigFileWriter.WriteAsync(path, original, bytes, cancellationToken);
            return new(true, endpoint, endpoint is null ? "已注销。请重载客户端配置。" : "注册成功。请重载客户端配置；导入后仍需在收件箱确认。");
        }

        /// <summary>读取客户端原始字节，缺失文件为未注册，文件错误不隐藏为默认值。</summary>
        private static async Task<byte[]?> ReadFileAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new SettingsPersistenceException("Codex配置无法读取，请检查文件权限后重试。", exception);
            }
        }

        /// <summary>保留UTF8 BOM语义，非法编码不允许被替换字符后覆盖。</summary>
        private static string Decode(byte[]? bytes)
        {
            if (bytes is null) return string.Empty;
            try { return Utf8.GetString(bytes, HasBom(bytes) ? 3 : 0, bytes.Length - (HasBom(bytes) ? 3 : 0)); }
            catch (DecoderFallbackException) { throw new SettingsConfigurationException("Codex配置不是有效UTF8，请先修正；原文件未修改。"); }
        }

        /// <summary>识别可选UTF8字节序标记，不改变其余文档编码。</summary>
        private static bool HasBom(byte[]? bytes) => bytes is not null && bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble());
    }
}
