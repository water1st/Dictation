using System.Collections.Generic;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 词性标签归并后的主类别、全部角色与动词活用类。
    /// </summary>
    internal sealed class JapaneseLexicalClassification
    {
        /// <summary>
        /// 表示 JMdict 词性标签归并后的主类别、全部角色与动词活用类。
        /// </summary>
        /// <param name="category">日语词条的词汇分类。</param>
        /// <param name="roles">该词条成立的词汇角色集合。</param>
        /// <param name="verbClass">动词活用类别，非动词使用对应空类别。</param>
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
