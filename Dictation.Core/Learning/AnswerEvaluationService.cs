using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 按语言比较规范化答案，不访问 UI 或持久化状态。
    /// </summary>
    internal sealed class AnswerEvaluationService
    {
        /// <summary>
        /// 去除首尾空白并做 Unicode NFC 规范化后比对答案；英语忽略大小写，日语精确匹配。
        /// </summary>
        /// <param name="submittedText">用户提交的答案；空白或空值判错。</param>
        /// <param name="acceptableAnswers">至少含一个非空元素的可接受答案集合。</param>
        /// <param name="language">决定大小写比较方式的学习语言。</param>
        /// <param name="cancellationToken">开始判题前检查的取消令牌。</param>
        /// <returns>规范化后的提交是否匹配任一可接受答案。</returns>
        public bool IsCorrect(
            string? submittedText,
            IReadOnlyList<string> acceptableAnswers,
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言不受支持。");
            }

            ArgumentNullException.ThrowIfNull(acceptableAnswers);
            if (acceptableAnswers.Count == 0)
            {
                throw new ArgumentException("至少需要一个可接受答案。", nameof(acceptableAnswers));
            }

            var normalizedAnswers = acceptableAnswers.Select(answer => NormalizeAnswer(answer, nameof(acceptableAnswers))).ToArray();
            if (string.IsNullOrWhiteSpace(submittedText))
            {
                return false;
            }

            var normalizedSubmission = NormalizeAnswer(submittedText, nameof(submittedText));
            var comparison = language == LearningLanguage.English
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return normalizedAnswers.Any(answer => string.Equals(answer, normalizedSubmission, comparison));
        }

        private static string NormalizeAnswer(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("答案不能为空。", parameterName);
            }

            return value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
