using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Statistics
{
    /// <summary>追加每日目标快照迁移，不改旧进度或补造历史打卡。</summary>
    internal static class StatisticsSchemaMigration
    {
        internal static SchemaMigration Definition { get; } = new(14, "保存按语言隔离的每日有效目标", """
            CREATE TABLE DailyPlanSnapshots (
                Language TEXT NOT NULL CHECK (Language IN ('Japanese','English')),
                LocalDate TEXT NOT NULL,
                LearningTarget INTEGER NOT NULL CHECK (LearningTarget>=0),
                ReviewTarget INTEGER NOT NULL CHECK (ReviewTarget>=0),
                KanaTarget INTEGER NOT NULL CHECK (KanaTarget>=0 AND (Language='Japanese' OR KanaTarget=0)),
                PRIMARY KEY (Language,LocalDate)
            );
            """);
    }
}
