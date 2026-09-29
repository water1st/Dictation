using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 为无需数据库的首页测试返回可控制的语言学习计划。
    /// </summary>
    internal sealed class StubLearningPlanReader : ILearningPlanReader
    {
        public Func<LearningLanguage, CancellationToken, Task<LearningPlanDto>> ReadAsync { get; set; } =
            (language, _) => Task.FromResult(new LearningPlanDto(
                language,
                20,
                20,
                LearningSelectionMode.Random,
                EnglishFormLabelMode.GrammarName,
                null,
                new[] { language == LearningLanguage.Japanese ? "Dictionary" : "Base" },
                new[] { LearningChannel.MeaningWriting },
                new[] { language == LearningLanguage.Japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin },
                DateTimeOffset.UtcNow,
                null));

        public Task<LearningPlanDto> LoadAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsync(language, cancellationToken);
        }
    }
}
