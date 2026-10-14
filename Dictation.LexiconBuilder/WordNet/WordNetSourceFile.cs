namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示参与构建及来源指纹计算的一个 WordNet 文件。
    /// </summary>
    internal readonly record struct WordNetSourceFile(
        string FullPath,
        string RelativePath,
        WordNetSourceFileKind Kind);
}
