using System.Text.Json;
using Dictation.Core.Configuration;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>纯转换复习会话、计划快照及持久化输入，不执行选词或状态更新。</summary>
    internal static class ReviewSessionMapper
    {
        /// <summary>将选定业务形式转成稳定义项种子，并用 BCL 序列化计划快照。</summary>
        /// <param name="sessionId">业务层生成的新会话标识。</param>
        /// <param name="plan">本轮启动时的计划。</param>
        /// <param name="groups">业务层已经排序和限额的整组。</param>
        /// <param name="now">创建 UTC 时间。</param>
        /// <param name="localDate">当前本地日期。</param>
        internal static CreateReviewSessionPo ToCreatePo(string sessionId, ReviewPlanDto plan, IReadOnlyList<ReviewSessionGroupSelection> groups,
            DateTimeOffset now, string localDate) => new(sessionId, plan.Language.ToString(),
                JsonSerializer.Serialize(plan), now, localDate, groups.Select(group => new ReviewSessionGroupSeedPo(
                    group.WordGroupId, group.Forms.Select(form => new ReviewSessionFormSeedPo(form.SourceSenseId, form.FormCode)).ToArray())).ToArray());

        /// <summary>恢复持久化启动计划，不再读取当前配置或重新查询例词。</summary>
        /// <param name="session">数据库读出的会话概要。</param>
        internal static ReviewSessionDto ToDto(ReviewSessionPo session)
        {
            try
            {
                var plan = JsonSerializer.Deserialize<ReviewPlanDto>(session.PlanJson)
                    ?? throw new InvalidDataException("复习会话的计划快照为空。");
                if (!Enum.TryParse<LearningLanguage>(session.Language, out var language)
                    || !Enum.IsDefined(language) || plan.Language != language)
                {
                    throw new InvalidDataException("复习会话与计划快照的语言不一致。");
                }
                return new ReviewSessionDto(session.SessionId, language, session.CreatedAtUtc, session.WordGroupIds, plan);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("复习会话的计划快照无法读取。", exception);
            }
        }
    }
}
