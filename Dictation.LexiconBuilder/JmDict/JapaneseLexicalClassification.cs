using System.Collections.Generic;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 词性标签归并后的主类别、全部角色与动词活用类。
    /// </summary>
    internal sealed class JapaneseLexicalClassification
    {
        public JapaneseLexicalClassification(
            JapaneseLexicalCategory category,
            IReadOnlyList<JapaneseLexicalRole> roles,
            JapaneseVerbClass verbClass)
        {
            Category = category;
            Roles = roles;
            VerbClass = verbClass;
        }

        public JapaneseLexicalCategory Category { get; }

        public IReadOnlyList<JapaneseLexicalRole> Roles { get; }

        public JapaneseVerbClass VerbClass { get; }
    }
}
