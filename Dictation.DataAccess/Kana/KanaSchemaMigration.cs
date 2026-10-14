using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Kana
{
    /// <summary>定义追加迁移13，不修改已发布的词汇表或前序迁移。</summary>
    internal static class KanaSchemaMigration
    {
        internal static SchemaMigration Definition { get; } = new(13, "保存独立五十音计划、记忆与听写观察", """
            CREATE TABLE KanaPlans (
                Id INTEGER PRIMARY KEY CHECK (Id=1),
                Enabled INTEGER NOT NULL CHECK (Enabled IN (0,1)),
                Hiragana INTEGER NOT NULL CHECK (Hiragana IN (0,1)),
                Katakana INTEGER NOT NULL CHECK (Katakana IN (0,1)),
                Clear INTEGER NOT NULL CHECK (Clear IN (0,1)),
                Voiced INTEGER NOT NULL CHECK (Voiced IN (0,1)),
                Contracted INTEGER NOT NULL CHECK (Contracted IN (0,1)),
                ReviewAll INTEGER NOT NULL CHECK (ReviewAll IN (0,1)),
                DailyTarget INTEGER NOT NULL CHECK (DailyTarget BETWEEN 1 AND 214),
                CHECK (Hiragana + Katakana >= 1),
                CHECK (Clear + Voiced + Contracted >= 1)
            );
            CREATE TABLE KanaMemories (
                KanaId TEXT PRIMARY KEY,
                Stability REAL NOT NULL CHECK (Stability>0),
                Difficulty REAL NOT NULL CHECK (Difficulty BETWEEN 1 AND 10),
                LastReviewedAtUtc TEXT NOT NULL,
                DueAtUtc TEXT NOT NULL,
                ReviewCount INTEGER NOT NULL CHECK (ReviewCount>=1),
                LapseCount INTEGER NOT NULL CHECK (LapseCount>=0)
            );
            CREATE TABLE KanaSessions (
                SessionId TEXT PRIMARY KEY,
                StartedLocalDate TEXT NOT NULL,
                Status TEXT NOT NULL CHECK (Status IN ('Active','Completed')),
                QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal>=0),
                CurrentKanaId TEXT NULL,
                CHECK ((Status='Active' AND CurrentKanaId IS NOT NULL) OR (Status='Completed' AND CurrentKanaId IS NULL))
            );
            CREATE UNIQUE INDEX UX_KanaSessions_Active ON KanaSessions(Status) WHERE Status='Active';
            CREATE TABLE KanaSessionItems (
                SessionId TEXT NOT NULL,
                KanaId TEXT NOT NULL,
                Text TEXT NOT NULL,
                Reading TEXT NOT NULL,
                RowHint TEXT NOT NULL,
                Script TEXT NOT NULL CHECK (Script IN ('Hiragana','Katakana')),
                Range TEXT NOT NULL CHECK (Range IN ('Clear','Voiced','Contracted')),
                QueueOrder INTEGER NOT NULL CHECK (QueueOrder>=0),
                HasWrong INTEGER NOT NULL DEFAULT 0 CHECK (HasWrong IN (0,1)),
                CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak>=0),
                NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal>=0),
                IsCompleted INTEGER NOT NULL DEFAULT 0 CHECK (IsCompleted IN (0,1)),
                CompletedLocalDate TEXT NULL,
                PRIMARY KEY (SessionId,KanaId),
                FOREIGN KEY (SessionId) REFERENCES KanaSessions(SessionId) ON DELETE CASCADE,
                CHECK ((IsCompleted=0 AND CompletedLocalDate IS NULL) OR (IsCompleted=1 AND CompletedLocalDate IS NOT NULL))
            );
            CREATE INDEX IX_KanaSessionItems_CompletedDate ON KanaSessionItems(CompletedLocalDate);
            CREATE TABLE KanaAttempts (
                SessionId TEXT NOT NULL,
                QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal>=0),
                KanaId TEXT NOT NULL,
                SubmittedText TEXT NOT NULL,
                WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
                AnsweredAtUtc TEXT NOT NULL,
                PRIMARY KEY (SessionId,QuestionOrdinal),
                FOREIGN KEY (SessionId,KanaId) REFERENCES KanaSessionItems(SessionId,KanaId) ON DELETE CASCADE
            );
            """);
    }
}
