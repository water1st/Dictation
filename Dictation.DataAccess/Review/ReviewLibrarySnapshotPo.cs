using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Review
{
    /// <summary>保存同一次数据库快照中的复习词组与已有成员词形记忆。</summary>
    public sealed record ReviewLibrarySnapshotPo(
        IReadOnlyList<ImportWordGroupPo> Groups,
        IReadOnlyList<ReviewStoredMemoryPo> Memories);
}
