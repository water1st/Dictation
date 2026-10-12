using Dictation.Core.Configuration;
using Dictation.DataAccess.Learning;

namespace Dictation.Core.Learning
{
    /// <summary>学习选词与每日目标共用的适用形式筛选，不消耗随机源或改变学习状态。</summary>
    internal static class LearningSessionSelectionEligibility
    {
        /// <summary>筛选计划启用的可输出形式，并按独立成员词形去重。</summary>
        /// <param name="candidate">原候选组与形式。</param>
        /// <param name="enabled">启用形式码。</param>
        /// <param name="outputs">允许输出方式。</param>
        /// <param name="language">当前语言。</param>
        internal static IReadOnlyList<LearningFormIdentityPo> GetForms(LearningSessionCandidatePo candidate,
            IReadOnlySet<string> enabled, IReadOnlyList<LearningOutputMode> outputs, LearningLanguage language) =>
            candidate.AvailableForms.Where(form => enabled.Contains(form.FormCode))
                .Where(form => language == LearningLanguage.English ? outputs.Contains(LearningOutputMode.Latin)
                    : outputs.Any(output => output switch
                    {
                        LearningOutputMode.Kana => form.ScriptCode == "Kana",
                        LearningOutputMode.Kanji => form.KanjiUsageCode == "Common" && form.ScriptCode == "Kanji",
                        _ => false
                    }))
                .DistinctBy(form => (form.MemberId, form.FormCode)).ToArray();

        /// <summary>只将至少一个适用形式未掌握的组作为可练候选。</summary>
        /// <param name="candidate">已有学习状态。</param>
        /// <param name="forms">已筛选的适用形式。</param>
        internal static bool NeedsPractice(LearningSessionCandidatePo candidate, IReadOnlyList<LearningFormIdentityPo> forms)
        {
            if (forms.Count == 0) return false;
            var states = candidate.FormStates.ToDictionary(state => (state.MemberId, state.FormCode), state => state.IsMastered);
            return !candidate.HasProgress || forms.Any(form => !states.TryGetValue((form.MemberId, form.FormCode), out var mastered) || !mastered);
        }
    }
}
