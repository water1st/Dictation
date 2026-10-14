namespace Dictation.DataAccess.Kana
{
    /// <summary>保存单份日语五十音计划的数据库列。</summary>
    public sealed class KanaPlanPo
    {
        public bool Enabled { get; set; }
        public bool Hiragana { get; set; }
        public bool Katakana { get; set; }
        public bool Clear { get; set; }
        public bool Voiced { get; set; }
        public bool Contracted { get; set; }
        public bool ReviewAll { get; set; }
        public int DailyTarget { get; set; }
    }
}
