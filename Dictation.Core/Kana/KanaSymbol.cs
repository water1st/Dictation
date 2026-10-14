namespace Dictation.Core.Kana
{
    /// <summary>保存一项固定语言事实，不是用户词库词条。</summary>
    /// <param name="Id">包含字形的稳定标识。</param>
    /// <param name="Text">要求书写的假名。</param>
    /// <param name="Reading">合成语音的平假名文本。</param>
    /// <param name="Script">答题字形。</param>
    /// <param name="Range">所属音类。</param>
    /// <param name="RowHint">只有同音项需要的行提示。</param>
    internal sealed record KanaSymbol(string Id, string Text, string Reading, KanaScript Script, KanaRange Range, string RowHint);
}
