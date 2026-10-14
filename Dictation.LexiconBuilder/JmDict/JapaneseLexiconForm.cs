using System;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示写入日语词库的词形、读音、语法角色与排序标记。
    /// </summary>
    internal sealed class JapaneseLexiconForm
    {
        /// <summary>
        /// 表示写入日语词库的词形、读音、语法角色与排序标记。
        /// </summary>
        /// <param name="surface">实际书写的词形表面文本。</param>
        /// <param name="reading">词形的假名读音。</param>
        /// <param name="formKind">该词形的语法形式。</param>
        /// <param name="scriptKind">词形采用的书写体系。</param>
        /// <param name="lexicalRole">词形在该义项中的词汇角色。</param>
        /// <param name="isCommon">来源是否标记为常用形式。</param>
        /// <param name="isPrimary">是否为该义项优先显示的形式。</param>
        public JapaneseLexiconForm(
            string surface,
            string reading,
            JapaneseFormKind formKind,
            JapaneseScriptKind scriptKind,
            JapaneseLexicalRole lexicalRole,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = string.IsNullOrWhiteSpace(reading)
                ? throw new ArgumentException("读音不能为空。", nameof(reading))
                : reading;
            FormKind = formKind;
            ScriptKind = scriptKind;
            LexicalRole = lexicalRole;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string Reading { get; }

        public JapaneseFormKind FormKind { get; }

        public JapaneseScriptKind ScriptKind { get; }

        public JapaneseLexicalRole LexicalRole { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
