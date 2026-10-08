using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Kana;
using System.Globalization;

namespace Dictation.Core.Kana
{
    /// <summary>编排五十音恢复、独立听写和每日目标。</summary>
    internal sealed class KanaSessionService : IKanaSessionReader, IKanaSessionOperations
    {
        private readonly IKanaPlanReader plans;
        private readonly KanaSessionDataAccessor data;
        private readonly FsrsScheduler fsrs;
        private readonly AnswerEvaluationService evaluation;
        private readonly KanaSessionPlanner planner;
        private readonly TimeProvider clock;

        /// <summary>组合原判题与FSRS、计划和独立持久化，不启动语音或访问词库。</summary>
        /// <param name="plans">读取已校验五十音计划。</param>
        /// <param name="data">独立轮次及记忆CRUD。</param>
        /// <param name="fsrs">已经验证的原FSRS纯算法。</param>
        /// <param name="evaluation">原NFC精确判题能力。</param>
        /// <param name="planner">五十音队列和重试规则。</param>
        /// <param name="clock">提供UTC与统一的实际本地日期。</param>
        public KanaSessionService(IKanaPlanReader plans, KanaSessionDataAccessor data, FsrsScheduler fsrs,
            AnswerEvaluationService evaluation, KanaSessionPlanner planner, TimeProvider clock)
        {
            this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.fsrs = fsrs ?? throw new ArgumentNullException(nameof(fsrs));
            this.evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }
        /// <inheritdoc />
        public async Task<KanaSessionDto?> StartOrResumeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = await data.GetActiveAsync(cancellationToken);
            if (active is not null) return KanaSessionMapper.ToDto(active, await data.GetItemsAsync(active.SessionId, cancellationToken));
            var plan = await plans.LoadAsync(cancellationToken);
            if (!plan.Enabled) return null;
            var symbols = KanaCatalog.Select(plan);
            var completed = (await data.GetCompletedIdsAsync(LocalDate(), cancellationToken)).ToHashSet();
            var memories = (await data.GetMemoriesAsync(cancellationToken)).ToDictionary(item => item.KanaId, KanaSessionMapper.ToState);
            var eligible = SelectEligible(symbols, completed, memories, plan.ReviewAll);
            var done = symbols.Count(symbol => completed.Contains(symbol.Id));
            var quantity = plan.ReviewAll ? eligible.Count : Math.Min(eligible.Count, Math.Max(0, plan.DailyTarget - done));
            if (quantity == 0) return null;
            var id = Guid.NewGuid().ToString("N");
            var items = planner.Shuffle(eligible.Take(quantity).ToArray()).Select((symbol, order) => KanaSessionMapper.ToItem(id, symbol, order)).ToArray();
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
            var items = await data.GetItemsAsync(session.SessionId, cancellationToken);
            return KanaSessionMapper.ToQuestion(session.SessionId, session.QuestionOrdinal, items.Single(item => item.KanaId == session.CurrentKanaId));
        }
        /// <inheritdoc />
        public async Task<KanaProgressDto> GetProgressAsync(CancellationToken cancellationToken = default)
        {
            var plan = await plans.LoadAsync(cancellationToken);
            var active = await data.GetActiveAsync(cancellationToken);
            var symbols = KanaCatalog.Select(plan);
            var completed = (await data.GetCompletedIdsAsync(LocalDate(), cancellationToken)).ToHashSet();
            var memories = (await data.GetMemoriesAsync(cancellationToken)).ToDictionary(item => item.KanaId, KanaSessionMapper.ToState);
            var done = symbols.Count(symbol => completed.Contains(symbol.Id));
            var available = SelectEligible(symbols, completed, memories, plan.ReviewAll).Count;
            var target = plan.ReviewAll ? symbols.Count : Math.Min(plan.DailyTarget, done + available);
            return new(plan.Enabled, done, plan.Enabled ? target : 0, available, active is not null);
        }
        /// <inheritdoc />
        public async Task<KanaFeedbackDto> SubmitAsync(string sessionId, int questionOrdinal, string? submittedText, CancellationToken cancellationToken = default)
        {
            if (questionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(questionOrdinal));
            var session = await RequireSessionAsync(sessionId, cancellationToken);
            if (session.Status != "Active" || session.QuestionOrdinal != questionOrdinal)
                throw new InvalidOperationException("题目已更新，请读取当前题后再作答。");
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
            await data.CommitAsync(session.SessionId, questionOrdinal, item,
                KanaSessionMapper.ToAttempt(session.SessionId, questionOrdinal, item.KanaId, submittedText, correct, now), next?.KanaId, memory, cancellationToken);
            var errors = KanaSessionMapper.ToErrors(await data.GetErrorsAsync(session.SessionId, cancellationToken));
            return new(correct, item.Text, next is null ? null : KanaSessionMapper.ToQuestion(session.SessionId, questionOrdinal + 1, next),
                next is null, items.Count(candidate => candidate.IsCompleted), items.Count, errors);
        }

        /// <summary>按未测／到期风险选择适用项目，全量忽略到期但不重复计入今日项目。</summary>
        /// <param name="symbols">计划启用范围。</param>
        /// <param name="completed">本地今天已完成标识。</param>
        /// <param name="memories">独立记忆状态。</param>
        /// <param name="all">全量模式是否忽略未来到期限制。</param>
        /// <returns>风险优先、未测优先的候选，后续才随机排题。</returns>
        private IReadOnlyList<KanaSymbol> SelectEligible(IReadOnlyList<KanaSymbol> symbols, HashSet<string> completed,
            IReadOnlyDictionary<string, FsrsMemoryState?> memories, bool all)
        {
            var now = clock.GetUtcNow();
            return symbols.Where(symbol => !completed.Contains(symbol.Id))
                .Where(symbol => all || !memories.TryGetValue(symbol.Id, out var state) || state is null || state.DueAtUtc <= now)
                .OrderBy(symbol => fsrs.GetRetrievability(memories.GetValueOrDefault(symbol.Id), now)).ThenBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray();
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
