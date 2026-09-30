using System.Globalization;
using Dictation.Core.Review;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>只格式化复习词组卡片及详情，风险和到期判断使用 Core 查询结果。</summary>
    public sealed class ReviewWordGroupItemViewModel
    {
        /// <summary>将业务查询结果适配为当前界面可绑定的文字。</summary>
        /// <param name="value">已经由 Core 计算风险的词组结果。</param>
        public ReviewWordGroupItemViewModel(ReviewWordGroupDto value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Forms = value.Forms.Select(form => new ReviewFormMemoryItemViewModel(form)).ToArray();
        }

        public ReviewWordGroupDto Value { get; }
        public string WordGroupId => Value.WordGroup.WordGroupId;
        public string HeadwordLabel => string.Join(" / ", Value.WordGroup.Members.Select(member => member.Headword));
        public string MeaningLabel => string.Join("；", Value.WordGroup.Members.SelectMany(member => member.Meanings).Distinct());
        public string AttributeLabel => string.Join(" / ", Value.WordGroup.Members.Select(member => $"{member.PartOfSpeechLabel} · {member.AttributeLabel}").Distinct());
        public string StateLabel => Value.NeedsInitialTest ? "待测" : Value.IsDue ? "已到期" : "未到期";
        public string NextReviewLabel => Value.NextReviewAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "尚未测试";
        public string RetentionLabel => Value.EstimatedRetention?.ToString("P0", CultureInfo.CurrentCulture) ?? "暂无预测";
        public IReadOnlyList<ReviewFormMemoryItemViewModel> Forms { get; }
    }
}
