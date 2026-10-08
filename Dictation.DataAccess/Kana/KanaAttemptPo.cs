namespace Dictation.DataAccess.Kana
{
    /// <summary>保存一次原始五十音作答和从项目快照读取的正确答案。</summary>
    public sealed class KanaAttemptPo
    {
        public string SessionId { get; set; } = "";
        public int QuestionOrdinal { get; set; }
        public string KanaId { get; set; } = "";
        public string SubmittedText { get; set; } = "";
        public bool WasCorrect { get; set; }
        public string AnsweredAtUtc { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
    }
}
