using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Statistics
{
    /// <summary>追加可删除的完成日事实，不复制掌握、答案或记忆状态。</summary>
    internal static class LearningCompletionHistoryMigration
    {
        internal static SchemaMigration Definition { get; } = new(15, "保留词组实际学习完成日期", """
            CREATE TABLE LearningCompletionDates (
                WordGroupId TEXT NOT NULL,
                LocalDate TEXT NOT NULL,
                PRIMARY KEY (WordGroupId,LocalDate),
                FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE
            );
            CREATE INDEX IX_LearningCompletionDates_LocalDate ON LearningCompletionDates(LocalDate);
            INSERT INTO LearningCompletionDates(WordGroupId,LocalDate)
                SELECT WordGroupId,CompletedLocalDate FROM LearningGroupProgress WHERE CompletedLocalDate IS NOT NULL;
            """);
    }
}
