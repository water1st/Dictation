using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>组合词组快照与 FSRS 计算，提供语言隔离的只读风险队列。</summary>
    internal sealed class ReviewLibraryService : IReviewLibraryQuery
    {
        private readonly ReviewLibraryDataAccessor dataAccessor;
        private readonly FsrsScheduler scheduler;
        private readonly TimeProvider timeProvider;

        /// <summary>组合持久化查询、纯记忆内核和系统时钟。</summary>
        /// <param name="dataAccessor">读取复习词库和状态。</param>
        /// <param name="scheduler">计算当前预计保持率。</param>
        /// <param name="timeProvider">提供本次快照的统一时间。</param>
        public ReviewLibraryService(ReviewLibraryDataAccessor dataAccessor, FsrsScheduler scheduler, TimeProvider timeProvider)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc />
        public async Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "复习词库语言不受支持。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await dataAccessor.LoadAsync(language.ToString(), cancellationToken);
            var now = timeProvider.GetUtcNow();
            var memories = snapshot.Memories.ToDictionary(memory =>
                (memory.WordGroupId, memory.SourceSenseId, memory.FormCode));
            var groups = new List<ReviewWordGroupDto>();
            foreach (var group in snapshot.Groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var forms = new List<ReviewFormMemoryDto>();
                foreach (var member in group.Members)
                {
                    // 汉字、假名和可接受变体是同一成员词形的输出，不重复制造记忆状态。
                    foreach (var form in member.Forms.DistinctBy(form => form.FormCode))
                    {
                        var state = memories.TryGetValue((group.WordGroupId, member.SourceSenseId, form.FormCode), out var memory)
                            ? ReviewLibraryMapper.ToState(memory) : null;
                        var retention = state is null ? (double?)null : scheduler.GetRetrievability(state, now);
                        forms.Add(ReviewLibraryMapper.ToFormDto(member, form, state, retention, state?.DueAtUtc <= now));
                    }
                }

                // 浏览全部形式不等于启用全部形式；尚未学习的其它形式不使已有记忆提前到期。
                var assessed = forms.Where(form => form.HasMemory).ToArray();
                var needsInitialTest = assessed.Length == 0;
                groups.Add(new ReviewWordGroupDto(ImportWordGroupMapper.ToDto(group), forms, needsInitialTest,
                    needsInitialTest || assessed.Any(form => form.IsDue),
                    assessed.Length == 0 ? null : assessed.Min(form => form.DueAtUtc),
                    assessed.Length == 0 ? null : assessed.Min(form => form.EstimatedRetention)));
            }

            var ordered = groups.OrderByDescending(group => group.NeedsInitialTest)
                .ThenBy(group => group.EstimatedRetention ?? 0)
                .ThenBy(group => group.NextReviewAtUtc)
                .ThenBy(group => group.WordGroup.CreatedAtUtc)
                .ThenBy(group => group.WordGroup.WordGroupId, StringComparer.Ordinal).ToArray();
            return new ReviewLibraryDto(language, now, ordered);
        }
    }
}
