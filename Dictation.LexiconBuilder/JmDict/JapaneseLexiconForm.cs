using System;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示写入日语词库的词形、读音、语法角色与排序标记。
    /// </summary>
    internal sealed class JapaneseLexiconForm
    {
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
