using Dictation.Core.Statistics;

namespace Dictation.Presentation.Mcp
{
    /// <summary>保留既有统计快照并标明计数和预测口径。</summary>
    internal sealed record McpLearningOverviewDto(LanguageStatisticsDto Statistics)
    {
        public bool EstimatedRetentionIsPrediction => true;
        public string CountUnit => "wordGroup";
        public string RetentionUnit => "probability";
    }
}
