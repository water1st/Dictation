using System.Globalization;
using Dictation.Core.Statistics;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>将到期点适配为原生柱条的日期、数量与相对高度。</summary>
    public sealed class StatisticsDueBarViewModel
    {
        /// <summary>依据本图最大值缩放高度，不重新计算到期业务。</summary>
        /// <param name="point">Core 计算的本地日到期组数。</param>
        /// <param name="maximum">本图最大组数。</param>
        public StatisticsDueBarViewModel(StatisticsTrendPointDto point, double maximum)
        {
            DateLabel = point.Date.ToString("MM/dd", CultureInfo.InvariantCulture);
            CountLabel = point.Value.ToString("0", CultureInfo.InvariantCulture);
            Height = maximum > 0 ? 100 * point.Value / maximum : 0;
        }
        public string DateLabel { get; }
        public string CountLabel { get; }
        public double Height { get; }
    }
}
