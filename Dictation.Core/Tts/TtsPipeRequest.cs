namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示通过命名管道发送给 TTS 宿主的请求。
    /// </summary>
    /// <param name="Type">请求消息类型。</param>
    /// <param name="Language">合成请求的语言；控制请求不携带语言。</param>
    /// <param name="Text">合成文本；控制请求不携带文本。</param>
    public sealed record TtsPipeRequest(
        TtsPipeMessageType Type,
        TtsLanguage? Language = null,
        string? Text = null);
}
