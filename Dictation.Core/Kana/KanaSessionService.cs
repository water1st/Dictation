using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Kana;
using System.Globalization;

namespace Dictation.Core.Kana
{
    /// <summary>编排五十音恢复、独立听写和每日目标。</summary>
    internal sealed class KanaSessionService : IKanaSessionReader, IKanaSessionOperations, IKanaProgressReader
    {
        private readonly IKanaPlanReader plans;
        private readonly KanaSessionDataAccessor data;
        private readonly FsrsScheduler fsrs;
        private readonly AnswerEvaluationService evaluation;
        private readonly KanaSessionPlanner planner;
        private readonly TimeProvider clock;
        private readonly KanaDailyAvailabilityService availability;
        private readonly Dictation.Core.Statistics.IDailyPlanSnapshotRecorder dailyTargets;

        /// <summary>组合原判题与FSRS、计划和独立持久化，不启动语音或访问词库。</summary>
        /// <param name="plans">读取已校验五十音计划。</param>
        /// <param name="data">独立轮次及记忆CRUD。</param>
        /// <param name="fsrs">已经验证的原FSRS纯算法。</param>
        /// <param name="evaluation">原NFC精确判题能力。</param>
        /// <param name="planner">五十音队列和重试规则。</param>
        /// <param name="clock">提供UTC与统一的实际本地日期。</param>
        /// <param name="availability">共享适用范围和每日进度查询。</param>
        /// <param name="dailyTargets">非空练习的当天目标记录。</param>
        public KanaSessionService(IKanaPlanReader plans, KanaSessionDataAccessor data, FsrsScheduler fsrs,
            AnswerEvaluationService evaluation, KanaSessionPlanner planner, TimeProvider clock,
            KanaDailyAvailabilityService availability, Dictation.Core.Statistics.IDailyPlanSnapshotRecorder dailyTargets)
        {
            this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.fsrs = fsrs ?? throw new ArgumentNullException(nameof(fsrs));
            this.evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.availability = availability ?? throw new ArgumentNullException(nameof(availability));
            this.dailyTargets = dailyTargets ?? throw new ArgumentNullException(nameof(dailyTargets));
        }
        /// <inheritdoc />
        public async Task<KanaSessionDto?> StartOrResumeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = await data.GetActiveAsync(cancellationToken);
            if (active is not null)
            {
                await dailyTargets.RecordTodayAsync(LearningLanguage.Japanese, cancellationToken);
                return KanaSessionMapper.ToDto(active, await data.GetItemsAsync(active.SessionId, cancellationToken));
            }
            var plan = await plans.LoadAsync(cancellationToken);
            if (!plan.Enabled) return null;
            var symbols = KanaCatalog.Select(plan);
            var completed = (await data.GetCompletedIdsAsync(LocalDate(), cancellationToken)).ToHashSet();
            var memories = (await data.GetMemoriesAsync(cancellationToken)).ToDictionary(item => item.KanaId, KanaSessionMapper.ToState);
            var eligible = availability.SelectEligible(symbols, completed, memories, plan.ReviewAll);
            var done = symbols.Count(symbol => completed.Contains(symbol.Id));
            var quantity = plan.ReviewAll ? eligible.Count : Math.Min(eligible.Count, Math.Max(0, plan.DailyTarget - done));
            if (quantity == 0) return null;
            await dailyTargets.RecordTodayAsync(LearningLanguage.Japanese, cancellationToken);
            var id = Guid.NewGuid().ToString("N");
            // 先随机打散同风险候选，再稳定按风险排序；不能先截断，再只随机题序。
            var now = clock.GetUtcNow();
            var selected = planner.Shuffle(eligible)
                .OrderBy(symbol => fsrs.GetRetrievability(memories.GetValueOrDefault(symbol.Id), now))
                .Take(quantity).ToArray();
            var items = planner.Shuffle(selected).Select((symbol, order) => KanaSessionMapper.ToItem(id, symbol, order)).ToArray();
            var created = await data.CreateAsync(new KanaSessionPo
            {
                SessionId = id, StartedLocalDate = LocalDate(), CurrentKanaId = items[0].KanaId
            }, items, cancellationToken);
            return KanaSessionMapper.ToDto(created, await data.GetItemsAsync(created.SessionId, cancellationToken));
        }
        /// <inheritdoc />
        public async Task<KanaQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var session = await RequireSessionAsync(sessionId, cancellationToken);
            if (session.Status == "Completed") return null;
            await dailyTargets.RecordTodayAsync(LearningLanguage.Japanese, cancellationToken);
            var items = await data.GetItemsAsync(session.SessionId, cancellationToken);
            return KanaSessionMapper.ToQuestion(session.SessionId, session.QuestionOrdinal, items.Single(item => item.KanaId == session.CurrentKanaId));
        }
        /// <inheritdoc />
        public Task<KanaProgressDto> GetProgressAsync(CancellationToken cancellationToken = default) => availability.GetProgressAsync(cancellationToken);
        /// <inheritdoc />
        public async Task<KanaFeedbackDto?> GetSubmissionFeedbackAsync(string sessionId, int questionOrdinal, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(questionOrdinal);
            var session = await RequireSessionAsync(sessionId, cancellationToken);
            var attempt = await data.GetAttemptAsync(session.SessionId, questionOrdinal, cancellationToken);
            if (attempt is null) return null;
            var items = await data.GetItemsAsync(session.SessionId, cancellationToken);
            var next = session.CurrentKanaId is null ? null : items.Single(item => item.KanaId == session.CurrentKanaId);
            var errors = KanaSessionMapper.ToErrors(await data.GetErrorsAsync(session.SessionId, cancellationToken));
            return new(attempt.WasCorrect, attempt.CorrectAnswer,
                next is null ? null : KanaSessionMapper.ToQuestion(session.SessionId, session.QuestionOrdinal, next),
                session.Status == "Completed", items.Count(item => item.IsCompleted), items.Count, errors);
        }
        /// <inheritdoc />
        public async Task<KanaFeedbackDto> SubmitAsync(string sessionId, int questionOrdinal, string? submittedText, CancellationToken cancellationToken = default)
        {
            if (questionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(questionOrdinal));
            var session = await RequireSessionAsync(sessionId, cancellationToken);
            if (session.Status != "Active" || session.QuestionOrdinal != questionOrdinal)
                throw new InvalidOperationException("题目已更新，请读取当前题后再作答。");
            await dailyTargets.RecordTodayAsync(LearningLanguage.Japanese, cancellationToken);
            var items = await data.GetItemsAsync(session.SessionId, cancellationToken);
            var item = items.Single(candidate => candidate.KanaId == session.CurrentKanaId);
            var correct = evaluation.IsCorrect(submittedText, new[] { item.Text }, LearningLanguage.Japanese, cancellationToken);
            var now = clock.GetUtcNow();
            planner.ApplyAnswer(item, correct, questionOrdinal, LocalDate());
            KanaMemoryPo? memory = null;
            if (item.IsCompleted)
            {
                var previous = (await data.GetMemoriesAsync(cancellationToken)).SingleOrDefault(candidate => candidate.KanaId == item.KanaId);
                memory = KanaSessionMapper.ToMemory(item.KanaId, fsrs.Schedule(KanaSessionMapper.ToState(previous), !item.HasWrong, now));
            }
            var next = planner.SelectNext(items, checked(questionOrdinal + 1));
            var attempt = KanaSessionMapper.ToAttempt(session.SessionId, questionOrdinal, item.KanaId, submittedText, correct, item.Text, now);
            // 所有反馈读取在提交前完成。提交成功之后不再因取消读取而丢失已保存结果。
            var previousErrors = await data.GetErrorsAsync(session.SessionId, cancellationToken);
            var errors = KanaSessionMapper.ToErrors(correct ? previousErrors : previousErrors.Append(attempt).ToArray());
            await data.CommitAsync(session.SessionId, questionOrdinal, item,
                attempt, next?.KanaId, memory, cancellationToken);
            return new(correct, item.Text, next is null ? null : KanaSessionMapper.ToQuestion(session.SessionId, questionOrdinal + 1, next),
                next is null, items.Count(candidate => candidate.IsCompleted), items.Count, errors);
        }

        /// <summary>统一校验已存在轮次，不把未知ID当空轮次吞掉。</summary>
        /// <param name="sessionId">调用方轮次标识。</param>
        /// <param name="token">取消读取令牌。</param>
        /// <returns>原持久化轮次。</returns>
        private async Task<KanaSessionPo> RequireSessionAsync(string sessionId, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            token.ThrowIfCancellationRequested();
            return await data.GetSessionAsync(sessionId.Trim(), token) ?? throw new InvalidOperationException("五十音轮次不存在。");
        }

        /// <summary>读取统一时钟的实际本地日期，不采用启动日作为完成日。</summary>
        /// <returns>用于持久化和每日统计的yyyy-MM-dd。</returns>
        private string LocalDate() => clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
