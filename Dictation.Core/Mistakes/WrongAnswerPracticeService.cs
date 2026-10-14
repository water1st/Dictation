using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Review;
using System.Globalization;

namespace Dictation.Core.Mistakes
{
    /// <summary>按真实错误与当前计划准备原复习会话，不修改全局计划或复制记忆算法。</summary>
    internal sealed class WrongAnswerPracticeService : IWrongAnswerPractice
    {
        private readonly IWrongAnswerQuery errors;
        private readonly IReviewPlanReader plans;
        private readonly IReviewLibraryQuery library;
        private readonly ReviewSessionDataAccessor sessions;
        private readonly TimeProvider clock;

        /// <summary>组合只读错误、计划、词库、既有会话持久化与统一时钟。</summary>
        /// <param name="errors">查询可归属的真实错误。</param>
        /// <param name="plans">读取当前启用范围。</param>
        /// <param name="library">查询同语言复习词组及原记忆。</param>
        /// <param name="sessions">复用原复习会话事务。</param>
        /// <param name="clock">提供UTC及本地日期。</param>
        public WrongAnswerPracticeService(IWrongAnswerQuery errors, IReviewPlanReader plans, IReviewLibraryQuery library,
            ReviewSessionDataAccessor sessions, TimeProvider clock)
        {
            this.errors = errors ?? throw new ArgumentNullException(nameof(errors));
            this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <inheritdoc />
        public async Task<ReviewSessionDto> StartOrResumeAsync(LearningLanguage language, IReadOnlyList<string> wordGroupIds,
            CancellationToken cancellationToken = default)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
                throw new ArgumentOutOfRangeException(nameof(language));
            ArgumentNullException.ThrowIfNull(wordGroupIds);
            cancellationToken.ThrowIfCancellationRequested();
            if (wordGroupIds.Count == 0 || wordGroupIds.Any(string.IsNullOrWhiteSpace)
                || wordGroupIds.Distinct(StringComparer.Ordinal).Count() != wordGroupIds.Count)
                throw new ArgumentException("请选择不同的复习错题词组。", nameof(wordGroupIds));
            var mistakes = await errors.LoadAsync(language, cancellationToken);
            var byGroup = mistakes.Where(item => item.Placement == "Review").ToLookup(item => item.WordGroupId);
            if (wordGroupIds.Any(id => !byGroup.Contains(id)))
                throw new ArgumentException("选择含已删除、非当前语言或尚在学习中的词组。", nameof(wordGroupIds));
            var active = await sessions.GetActiveAsync(language.ToString(), cancellationToken);
            if (active is not null) return ReviewSessionMapper.ToDto(active);
            var plan = await plans.LoadAsync(language, cancellationToken);
            if (wordGroupIds.Count > plan.SessionWordLimit || (plan.RangeMode == ReviewRangeMode.Selected
                && wordGroupIds.Any(id => !plan.WordGroupIds.Contains(id, StringComparer.Ordinal))))
                throw new ArgumentException("选择超出本轮数量或复习计划范围。", nameof(wordGroupIds));
            var snapshot = await library.LoadAsync(language, cancellationToken);
            var groups = new List<ReviewSessionGroupSelection>();
            foreach (var id in wordGroupIds)
            {
                var group = snapshot.Groups.Single(value => value.WordGroup.WordGroupId == id);
                var wrongForms = byGroup[id].Select(item => (item.SourceSenseId, item.FormCode)).ToHashSet();
                var forms = group.Forms.Where(form => wrongForms.Contains((form.SourceSenseId, form.FormCode))
                    && plan.FormCodes.Contains(form.FormCode, StringComparer.Ordinal)
                    && group.WordGroup.Members.Any(member => member.SourceSenseId == form.SourceSenseId
                        && PracticeFormApplicability.IsFormApplicable(language, member.IsPhrase, form.FormCode)
                        && plan.OutputModes.Any(output => PracticeFormApplicability.HasOutput(language, member.KanjiUsageCode,
                            member.Forms.Where(value => value.FormCode == form.FormCode).Select(value => value.ScriptCode), output))))
                    .ToArray();
                if (forms.Length == 0) throw new InvalidOperationException("选中组的错误形式未在复习计划中启用。");
                groups.Add(new ReviewSessionGroupSelection(id, forms));
            }
            // 主动重练允许未来到期记忆；创建仍用原事务，竞争启动返回已有胜者，绝不清空旧会话。
            var created = await sessions.CreateAsync(ReviewSessionMapper.ToCreatePo(Guid.NewGuid().ToString("N"), plan,
                groups, clock.GetUtcNow(), clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), cancellationToken);
            return ReviewSessionMapper.ToDto(created);
        }
    }
}
