namespace Dictation.DataAccess.Runtime
{
    public sealed record SchemaMigration(int Version, string Name, string Sql);
}
