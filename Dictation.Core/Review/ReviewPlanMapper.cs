using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>只转换复习计划请求、持久化字段和 DTO，不校验词库、不执行数据库操作。</summary>
    internal static class ReviewPlanMapper
    {
        /// <summary>将已规范化的保存请求转换为独立复习计划快照。</summary>
        /// <param name="request">业务层已经校验的请求。</param>
        /// <param name="now">计划保存时间。</param>
        /// <returns>可由 DAL 持久化的快照。</returns>
        internal static ReviewPlanPo ToPo(SaveReviewPlanRequestDto request, DateTimeOffset now) => new(
            request.Language.ToString(), request.DailyReviewTarget, request.SessionWordLimit, request.RangeMode.ToString(),
            request.WordGroupIds.ToArray(), request.FormCodes.ToArray(), request.Channels.Select(value => value.ToString()).ToArray(),
            request.OutputModes.Select(value => value.ToString()).ToArray(), request.EnglishFormLabelMode.ToString(),
            request.EnglishExampleSourceSenseId, now);

        /// <summary>投影持久化计划并附加由业务层解析的英语示例范式。</summary>
        /// <param name="plan">独立复习计划的持久化快照。</param>
        /// <param name="example">已解析范式，非示例模式为空。</param>
        /// <returns>强类型复习计划。</returns>
        internal static ReviewPlanDto ToDto(ReviewPlanPo plan, EnglishExampleParadigmDto? example) => new(
            Parse<LearningLanguage>(plan.Language), plan.DailyReviewTarget, plan.SessionWordLimit, Parse<ReviewRangeMode>(plan.RangeMode),
            plan.WordGroupIds.ToArray(), plan.FormCodes.ToArray(), plan.Channels.Select(Parse<LearningChannel>).ToArray(),
            plan.OutputModes.Select(Parse<LearningOutputMode>).ToArray(), Parse<EnglishFormLabelMode>(plan.EnglishFormLabelMode),
            plan.EnglishExampleSourceSenseId, plan.UpdatedAtUtc, example);

        private static T Parse<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed : throw new InvalidDataException($"复习计划中的 {typeof(T).Name} 无效：{value}。");
    }
}
