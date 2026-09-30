using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>将完成学习的启用形式及本会话历史转换为初次客观记忆评价。</summary>
    internal sealed class LearningMemorySeedPlanner
    {
        private readonly FsrsScheduler scheduler;

        /// <summary>组合已验证的纯 FSRS 数值内核，不读写数据库。</summary>
        /// <param name="scheduler">计算初次 Good／Again 状态的内核。</param>
        public LearningMemorySeedPlanner(FsrsScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>只为本次完成且有客观作答的形式准备种子，任何一次答错保持 Again。</summary>
        /// <param name="completedForms">调用方筛选的本次完成组的启用词形。</param>
        /// <param name="history">本会话题目及客观判定。</param>
        /// <param name="now">本次完成学习的时间。</param>
        /// <returns>供同一答题事务首次写入的记忆种子。</returns>
        internal IReadOnlyList<ReviewFormMemoryPo> CreateSeeds(IReadOnlyList<LearningFormUnitPo> completedForms,
            IReadOnlyList<LearningQuestionHistoryPo> history, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(completedForms);
            ArgumentNullException.ThrowIfNull(history);
            var outcomes = history.Where(question => question.WasCorrect.HasValue)
                .ToLookup(question => (question.WordGroupId, question.MemberId, question.FormCode));
            var seeds = new List<ReviewFormMemoryPo>();
            foreach (var form in completedForms.Where(form => form.IsMastered))
            {
                var answers = outcomes[(form.WordGroupId, form.MemberId, form.FormCode)];
                if (!answers.Any())
                {
                    continue;
                }

                // 重练正确不抹去本轮错误；其它成员或词形的错误不影响这一形式。
                var state = scheduler.Schedule(null, answers.All(answer => answer.WasCorrect == true), now);
                seeds.Add(ReviewMemoryMapper.ToPo(form.WordGroupId, form.MemberId, form.FormCode, state));
            }

            return seeds;
        }
    }
}
