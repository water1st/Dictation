using System;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 解释 JMdict priority 标记是否代表常用词形。
    /// </summary>
    internal static class JmDictPriority
    {
        public static bool IsCommon(string priority)
        {
            return priority.StartsWith("news", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("ichi", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("spec", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("gai", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("nf", StringComparison.OrdinalIgnoreCase);
        }
    }
}
