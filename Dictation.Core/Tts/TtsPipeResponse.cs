namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示 TTS 宿主通过命名管道返回的响应。
    /// </summary>
    /// <param name="Type">响应消息类型。</param>
    /// <param name="Payload">音频响应的内存字节；非音频响应不携带该值。</param>
    /// <param name="Error">错误响应的说明；非错误响应不携带该值。</param>
    public sealed record TtsPipeResponse(
        TtsPipeMessageType Type,
        byte[]? Payload = null,
        string? Error = null);
}
