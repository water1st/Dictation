using System;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示英语词形、语法类别及其来源。
    /// </summary>
    internal sealed class EnglishForm
    {
        public EnglishForm(string surface, EnglishFormKind kind, EnglishFormSourceKind sourceKind)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("英语词形不能为空。", nameof(surface))
                : surface;
            Kind = kind;
            SourceKind = sourceKind;
        }

        public string Surface { get; }

        public EnglishFormKind Kind { get; }

        public EnglishFormSourceKind SourceKind { get; }
    }
}
