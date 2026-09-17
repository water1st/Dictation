using System;

namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 表示词库来源、解析或数据库构建失败。
    /// </summary>
    internal sealed class LexiconBuildException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        public LexiconBuildException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public LexiconBuildException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
