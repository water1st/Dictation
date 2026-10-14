using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Core.Practice
{
    /// <summary>只提供出题所需的形式、渠道、字形和提示标签能力，不暴露计划管理操作。</summary>
    internal interface IPracticeQuestionPlan
    {
        IReadOnlyList<string> FormCodes { get; }
        IReadOnlyList<LearningChannel> Channels { get; }
        IReadOnlyList<LearningOutputMode> OutputModes { get; }
        EnglishFormLabelMode EnglishFormLabelMode { get; }
        EnglishExampleParadigmDto? EnglishExampleParadigm { get; }
    }
}
