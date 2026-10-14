using Dictation.DataAccess.Lexicon;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 将持久化的中文释义元数据转换为稳定枚举。
    /// </summary>
    internal static class LexiconMeaningMetadataMapper
    {
        /// <summary>
        /// 解析中文释义来源代码。
        /// </summary>
        /// <param name="value">持久化的来源代码。</param>
        /// <returns>对应的中文释义来源。</returns>
        /// <exception cref="LexiconDataAccessException">代码无法解析时抛出。</exception>
        internal static LexiconMeaningSource ParseSource(string value)
        {
            return Enum.TryParse<LexiconMeaningSource>(value, out var source)
                ? source
                : throw CreateInvalidMetadataException("来源", value);
        }

        /// <summary>
        /// 解析中文释义精度代码。
        /// </summary>
        /// <param name="value">持久化的精度代码。</param>
        /// <returns>对应的中文释义精度。</returns>
        /// <exception cref="LexiconDataAccessException">代码无法解析时抛出。</exception>
        internal static LexiconMeaningPrecision ParsePrecision(string value)
        {
            return Enum.TryParse<LexiconMeaningPrecision>(value, out var precision)
                ? precision
                : throw CreateInvalidMetadataException("精度", value);
        }

        private static LexiconDataAccessException CreateInvalidMetadataException(
            string fieldName,
            string value)
        {
            var reason = new InvalidDataException($"不支持的中文释义{fieldName}：{value}。");
            return new LexiconDataAccessException("词库包含无效的中文释义元数据。", reason);
        }
    }
}
