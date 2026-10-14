using System.Windows;
using System.Windows.Media;
using Dictation.Core.Configuration;
using Dictation.Core.Statistics;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>将只读报表适配为卡片和图表坐标，不承担统计或记忆算法。</summary>
    public sealed class StatisticsReportViewModel
    {
        private readonly LanguageStatisticsDto report;

        /// <summary>保存业务结果并生成仅用于布局的图点与柱条。</summary>
        /// <param name="report">当前语言的完整业务报表。</param>
        public StatisticsReportViewModel(LanguageStatisticsDto report)
        {
            this.report = report ?? throw new ArgumentNullException(nameof(report));
            RetentionPoints = new PointCollection(report.RetentionTrend.Select((point, index) => new Point(
                report.RetentionTrend.Count > 1 ? 600.0 * index / (report.RetentionTrend.Count - 1) : 0, 140 * (1 - point.Value))));
            RetentionPoints.Freeze();
            var maximum = report.DueTrend.Count > 0 ? report.DueTrend.Max(point => point.Value) : 0;
            DueBars = report.DueTrend.Select(point => new StatisticsDueBarViewModel(point, maximum)).ToArray();
        }

        public LearningLanguage Language => report.Language;
        public long LearnedGroupCount => report.LearnedGroupCount;
        public long TodayLearnedGroupCount => report.TodayLearnedGroupCount;
        public long TodayReviewedGroupCount => report.TodayReviewedGroupCount;
        public long PendingTestGroupCount => report.PendingTestGroupCount;
        public long OverdueGroupCount => report.OverdueGroupCount;
        public IReadOnlyList<FrequentMistakeDto> TopMistakes => report.TopMistakes;
        public string MistakesDescription => report.TopMistakes.Count == 0 ? "还没有错题记录。" : "累计易错词 · 最多十组";
        public string RetentionDescription => report.RetentionTrend.Count == 0 ? "尚无客观记忆记录，完成练习后再看预计保持率。" : "FSRS 预计保持率 · 假定不再复习，不是实际测量值";
        public string RetentionStartLabel => report.RetentionTrend.Count == 0 ? string.Empty : report.RetentionTrend[0].Date.ToString("MM/dd");
        public string RetentionEndLabel => report.RetentionTrend.Count == 0 ? string.Empty : report.RetentionTrend[^1].Date.ToString("MM/dd");
        public string DueDescription => "未来十四天首次到期的词组；待测与逾期单列，不重复累计。";
        public PointCollection RetentionPoints { get; }
        public IReadOnlyList<StatisticsDueBarViewModel> DueBars { get; }
    }
}
