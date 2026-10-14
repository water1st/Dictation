using Dictation.Core.Learning;

namespace Dictation.Core.Mistakes
{
    /// <summary>提供一次原始错误的只读详情，不代表该错误仍未纠正。</summary>
    /// <param name="Mode">Learning或Review来源模式。</param>
    /// <param name="Channel">当时要求的题型。</param>
    /// <param name="OutputMode">当时要求的字形。</param>
    /// <param name="SubmittedText">规范化后的实际作答。</param>
    /// <param name="AnsweredAtUtc">作答UTC时间。</param>
    /// <param name="ExpectedAnswer">用户词库快照中该形式和字形的标准答案。</param>
    /// <param name="ExpectedSpokenText">浏览时可播放的对应目标读音。</param>
    public sealed record WrongAnswerAttemptDto(string Mode, LearningChannel Channel, LearningOutputMode OutputMode,
        string SubmittedText, DateTimeOffset AnsweredAtUtc, string ExpectedAnswer, string ExpectedSpokenText);
}
