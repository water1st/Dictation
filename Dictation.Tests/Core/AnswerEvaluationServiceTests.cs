using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证日英语答案规范化、大小写比较、非法输入及取消边界。
    /// </summary>
    public sealed class AnswerEvaluationServiceTests
    {
        private readonly AnswerEvaluationService service = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsCorrect_NullOrBlankSubmissionReturnsFalse(string? submittedText)
        {
            Assert.False(service.IsCorrect(
                submittedText,
                new[] { "gone" },
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsCorrect_TrimsNormalizesNfcAndIgnoresEnglishCase()
        {
            Assert.True(service.IsCorrect(
                "  GONE  ",
                new[] { "gone" },
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.True(service.IsCorrect(
                "か\u3099",
                new[] { "が" },
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsCorrect_AcceptsAnyAuthorityVariant()
        {
            Assert.True(service.IsCorrect(
                "learnt",
                new[] { "learned", "learnt" },
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsCorrect_JapaneseRequiresExactConfiguredScript()
        {
            Assert.False(service.IsCorrect(
                "食べます",
                new[] { "たべます" },
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken));
            Assert.True(service.IsCorrect(
                "たべます",
                new[] { "たべます" },
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData("goen")]
        [InlineData("gone!")]
        [InlineData("go ne")]
        public void IsCorrect_DoesNotAcceptFuzzySpelling(string submittedText)
        {
            Assert.False(service.IsCorrect(
                submittedText,
                new[] { "gone" },
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsCorrect_RejectsMissingAnswerKeyAndUnknownLanguage()
        {
            Assert.Throws<ArgumentNullException>(() => service.IsCorrect(
                "gone",
                null!,
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => service.IsCorrect(
                "gone",
                Array.Empty<string>(),
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => service.IsCorrect(
                "gone",
                new[] { " " },
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.IsCorrect(
                "gone",
                new[] { "gone" },
                (LearningLanguage)999,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void IsCorrect_PreCanceledTokenThrows()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => service.IsCorrect(
                "gone",
                new[] { "gone" },
                LearningLanguage.English,
                cancellation.Token));
        }
    }
}
