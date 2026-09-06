using Dictation.Services.Configuration;

namespace Dictation.Services.Learning
{
    public sealed class AnswerEvaluationService
    {
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
