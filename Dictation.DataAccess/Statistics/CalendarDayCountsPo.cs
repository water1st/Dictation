namespace Dictation.DataAccess.Statistics
{
    /// <summary>单个语言和本地日期的完成事实与可空冻结目标，用于月区间查询。</summary>
    public sealed class CalendarDayCountsPo
    {
        /// <summary>持久化语言码。</summary>
        public string Language { get; internal set; } = string.Empty;
        /// <summary>严格本地日期。</summary>
        public string LocalDate { get; internal set; } = string.Empty;
        /// <summary>去重学习完成数。</summary>
        public long LearningCompleted { get; internal set; }
        /// <summary>去重复习完成数。</summary>
        public long ReviewCompleted { get; internal set; }
        /// <summary>独立五十音项目数。</summary>
        public long KanaCompleted { get; internal set; }
        /// <summary>冻结学习目标；空表示没有目标证据。</summary>
        public long? LearningTarget { get; internal set; }
        /// <summary>冻结复习目标。</summary>
        public long? ReviewTarget { get; internal set; }
        /// <summary>冻结五十音目标。</summary>
        public long? KanaTarget { get; internal set; }
    }
}
