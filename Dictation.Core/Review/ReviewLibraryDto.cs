using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>表示一次按语言隔离的复习词库查询，计数以词组而非词形为单位。</summary>
    public sealed record ReviewLibraryDto(LearningLanguage Language, DateTimeOffset AsOfUtc,
        IReadOnlyList<ReviewWordGroupDto> Groups)
    {
        public int TotalGroupCount => Groups.Count;
        public int PendingTestGroupCount => Groups.Count(group => group.NeedsInitialTest);
        public int DueGroupCount => Groups.Count(group => group.IsDue);
    }
}
