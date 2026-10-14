using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示英语 lexeme 下按源顺序排列的一个 WordNet 词义。
    /// </summary>
    internal sealed class EnglishSense
    {
        /// <summary>
        /// 表示英语 lexeme 下按源顺序排列的一个 WordNet 词义。
        /// </summary>
        /// <param name="senseId">稳定来源义项标识。</param>
        /// <param name="synsetId">所属同义词集标识。</param>
        /// <param name="order">义项在来源中的顺序。</param>
        /// <param name="derivationSenseIds">直接派生关联的来源义项标识。</param>
        public EnglishSense(
            string senseId,
            string synsetId,
            int order,
            IReadOnlyList<string> derivationSenseIds)
        {
            SenseId = RequireText(senseId, nameof(senseId));
            SynsetId = RequireText(synsetId, nameof(synsetId));
            if (order <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            Order = order;
            DerivationSenseIds = derivationSenseIds ?? throw new ArgumentNullException(nameof(derivationSenseIds));
        }

        public string SenseId { get; }

        public string SynsetId { get; }

        public int Order { get; }

        public IReadOnlyList<string> DerivationSenseIds { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("WordNet 文本不能为空。", parameterName)
                : value;
        }
    }
}
