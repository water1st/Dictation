namespace Dictation.Core.Kana
{
    /// <summary>保留本轮原错误，重答正确不会抹掉审计。</summary>
    /// <param name="QuestionOrdinal">原错误题序。</param>
    /// <param name="KanaId">对应项目。</param>
    /// <param name="SubmittedText">当时提交。</param>
    /// <param name="CorrectAnswer">当时正确字形。</param>
    public sealed record KanaErrorDto(int QuestionOrdinal, string KanaId, string SubmittedText, string CorrectAnswer);
}
