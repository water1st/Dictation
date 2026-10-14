namespace Dictation.Core.Kana
{
    /// <summary>提供题型、字形、必要行提示与语音，不提供待显示的正确答案。</summary>
    /// <param name="SessionId">所属轮次。</param>
    /// <param name="QuestionOrdinal">用于拒绝重复提交的题序。</param>
    /// <param name="KanaId">内部稳定项目标识，不呈现在题面。</param>
    /// <param name="Script">要求书写的字形。</param>
    /// <param name="Range">音类。</param>
    /// <param name="RowHint">同音项的必要行提示。</param>
    /// <param name="SpokenText">仅供内存语音合成的文本，不显示为答案。</param>
    public sealed record KanaQuestionDto(string SessionId, int QuestionOrdinal, string KanaId, KanaScript Script,
        KanaRange Range, string RowHint, string SpokenText);
}
