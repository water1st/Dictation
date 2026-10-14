namespace Dictation.DataAccess.Runtime
{
    /// <summary>
    /// 描述一个有序执行的 SQLite 结构迁移。
    /// </summary>
    /// <param name="Version">严格递增的正版本号。</param>
    /// <param name="Name">用于记录和诊断的迁移名称。</param>
    /// <param name="Sql">在迁移事务内执行的 SQL。</param>
    public sealed record SchemaMigration(int Version, string Name, string Sql);
}
