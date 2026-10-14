namespace Dictation.Core.Tts
{
    /// <summary>
    /// 定义 TTS 主进程与宿主进程之间的管道消息类型。
    /// </summary>
    public enum TtsPipeMessageType
    {
        Ping = 1,
        Synthesize = 2,
        Shutdown = 3,
        Pong = 4,
        Audio = 5,
        Error = 6,
        ShutdownAcknowledged = 7
    }
}
