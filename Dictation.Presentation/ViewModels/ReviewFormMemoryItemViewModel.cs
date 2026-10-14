using System.Globalization;
using Dictation.Core.Review;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>将一个成员词形的记忆信息格式化为本地时间和中文状态，不重新计算风险。</summary>
    public sealed class ReviewFormMemoryItemViewModel
    {
        /// <summary>适配已经确定的词形记忆结果。</summary>
        /// <param name="value">Core 返回的成员词形状态。</param>
        public ReviewFormMemoryItemViewModel(ReviewFormMemoryDto value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ReviewFormMemoryDto Value { get; }
        public string FormLabel => $"{Value.Headword} · {Value.FormLabel}";
        public string StateLabel => !Value.HasMemory ? "尚未评价" : Value.IsDue ? "已到期" : "未到期";
        public string DueLabel => Value.DueAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "尚未评价";
        public string RetentionLabel => Value.EstimatedRetention?.ToString("P0", CultureInfo.CurrentCulture) ?? "暂无预测";
        public string CountsLabel => $"评价 {Value.ReviewCount} 次 · 错误 {Value.LapseCount} 次";
    }
}
