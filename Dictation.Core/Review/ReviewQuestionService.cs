using Dictation.Core.Practice;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>编排复习题目的纯调度、持久化和计划快照提示。</summary>
    internal sealed class ReviewQuestionService : IReviewQuestionReader
    {
        private readonly ReviewQuestionDataAccessor dataAccessor;
        private readonly PracticeQuestionScheduler scheduler;

        /// <summary>组合独立复习持久化与两种模式共用的纯调度能力。</summary>
        /// <param name="dataAccessor">只访问复习本轮状态的数据访问器。</param>
        /// <param name="scheduler">不读取或修改数据库的共享调度组件。</param>
        public ReviewQuestionService(ReviewQuestionDataAccessor dataAccessor, PracticeQuestionScheduler scheduler)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <inheritdoc />
        public async Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await dataAccessor.GetSnapshotAsync(sessionId.Trim(), cancellationToken);
            if (snapshot.Status == "Completed" || snapshot.Units.Count == 0)
            {
                return null;
            }
            // 使用本轮快照而非当前设置，避免修改计划改变待答题及覆盖要求。
            var session = ReviewSessionMapper.ToDto(snapshot.Session);
            var pending = snapshot.PendingQuestion;
            if (pending is null)
            {
                var scheduled = scheduler.CreateNextQuestion(session.SessionId, session.Language, snapshot.Units,
                    snapshot.History, session.PlanSnapshot, snapshot.CurrentOrdinal)
                    ?? throw new InvalidOperationException("复习会话尚未完成，但没有可调度的题目。");
                pending = await dataAccessor.CreatePendingAsync(scheduled, cancellationToken);
            }
            return PracticeQuestionPresenter.Create(session.Language, snapshot.Units, pending, session.PlanSnapshot);
        }
    }
}
