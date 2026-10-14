namespace Dictation.Core.Configuration
{
    /// <summary>描述本应用Codex注册项的状态及本次是否修改配置。</summary>
    /// <param name="Changed">本次是否实际写入配置文件。</param>
    /// <param name="Endpoint">现有本应用端点；未注册时为空。</param>
    /// <param name="Message">用户可阅读的操作结果，不包含配置内容或凭据。</param>
    public sealed record CodexMcpRegistrationResultDto(bool Changed, Uri? Endpoint, string Message);
}
