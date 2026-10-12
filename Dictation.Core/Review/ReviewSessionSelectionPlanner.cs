using Dictation.Core.Learning;

namespace Dictation.Core.Review
{
    /// <summary>只在计划启用且适用的形式中按待测／风险选组，不安排未来记忆或修改状态。</summary>
    internal sealed class ReviewSessionSelectionPlanner
    {
        /// <summary>先约束范围及适用形式，再按风险排序并限制整组数量。</summary>
        /// <param name="library">同语言完整词组与当前记忆风险快照。</param>
        /// <param name="plan">本次启动的独立复习计划。</param>
        /// <param name="remainingDailyCount">当天尚可完成的不同整组数。</param>
        /// <returns>本轮有序整组与其中待测或到期形式。</returns>
        internal IReadOnlyList<ReviewSessionGroupSelection> Select(ReviewLibraryDto library, ReviewPlanDto plan, int remainingDailyCount)
        {
            if (remainingDailyCount <= 0)
            {
                return Array.Empty<ReviewSessionGroupSelection>();
            }
            return GetCandidates(library, plan).Take(Math.Min(remainingDailyCount, plan.SessionWordLimit)).ToArray();
        }

        /// <summary>提供未经轮次数量截断的同一适用队列，供每日目标和实际选词共用。</summary>
        /// <param name="library">完整风险快照。</param>
        /// <param name="plan">启用范围、形式及输出要求。</param>
        internal IReadOnlyList<ReviewSessionGroupSelection> GetCandidates(ReviewLibraryDto library, ReviewPlanDto plan)
        {
            var scope = plan.WordGroupIds.ToHashSet(StringComparer.Ordinal);
            var enabled = plan.FormCodes.ToHashSet(StringComparer.Ordinal);
            var candidates = library.Groups
                .Where(group => plan.RangeMode == ReviewRangeMode.All || scope.Contains(group.WordGroup.WordGroupId))
                .Select(group => new
                {
                    Group = group.WordGroup,
                    Forms = group.Forms.Where(form => enabled.Contains(form.FormCode) && (!form.HasMemory || form.IsDue)
                        && group.WordGroup.Members.Any(member => member.SourceSenseId == form.SourceSenseId
                            && PracticeFormApplicability.IsFormApplicable(plan.Language, member.IsPhrase, form.FormCode)
                            && plan.OutputModes.Any(output => PracticeFormApplicability.HasOutput(plan.Language,
                                member.KanjiUsageCode, member.Forms.Where(variant => variant.FormCode == form.FormCode).Select(variant => variant.ScriptCode), output))))
                        .ToArray()
                })
                .Where(candidate => candidate.Forms.Length > 0)
                .OrderByDescending(candidate => candidate.Forms.Any(form => !form.HasMemory))
                .ThenBy(candidate => candidate.Forms.Min(form => form.EstimatedRetention ?? 0))
                .ThenBy(candidate => candidate.Forms.Min(form => form.DueAtUtc))
                .ThenBy(candidate => candidate.Group.CreatedAtUtc)
                .ThenBy(candidate => candidate.Group.WordGroupId, StringComparer.Ordinal);
            return candidates.Select(candidate => new ReviewSessionGroupSelection(candidate.Group.WordGroupId, candidate.Forms)).ToArray();
        }
    }
}
