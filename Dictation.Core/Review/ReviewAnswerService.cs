using System.Globalization;
using System.Text;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>编排复习客观判题、间隔重练和完成形式的记忆提交。</summary>
    internal sealed class ReviewAnswerService : IReviewSessionAnswerer
    {
        private readonly ReviewQuestionDataAccessor questions;
        private readonly ReviewAnswerDataAccessor answers;
        private readonly PracticeQuestionScheduler scheduler;
        private readonly AnswerEvaluationService evaluation;
        private readonly ReviewMemoryUpdatePlanner memoryPlanner;
        private readonly TimeProvider timeProvider;

        /// <summary>组合独立复习持久化及已有纯判题、调度、记忆能力。</summary>
        /// <param name="questions">读取一致的复习快照。</param>
        /// <param name="answers">原子写入本次成绩与下一题。</param>
        /// <param name="scheduler">共用出题及重练规则。</param>
        /// <param name="evaluation">共用客观文本判题。</param>
        /// <param name="memoryPlanner">续算完成形式的 FSRS。</param>
        /// <param name="timeProvider">提供 UTC 评价时间及本地完成日期。</param>
        public ReviewAnswerService(ReviewQuestionDataAccessor questions, ReviewAnswerDataAccessor answers,
            PracticeQuestionScheduler scheduler, AnswerEvaluationService evaluation, ReviewMemoryUpdatePlanner memoryPlanner, TimeProvider timeProvider)
        {
            this.questions = questions ?? throw new ArgumentNullException(nameof(questions));
            this.answers = answers ?? throw new ArgumentNullException(nameof(answers));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
            this.memoryPlanner = memoryPlanner ?? throw new ArgumentNullException(nameof(memoryPlanner));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc />
        public async Task<ReviewAnswerResultDto> SubmitAnswerAsync(SubmitReviewAnswerRequestDto request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.QuestionId);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await questions.GetSnapshotAsync(request.SessionId.Trim(), cancellationToken);
            var pending = snapshot.PendingQuestion;
            if (snapshot.Status != "InProgress" || pending is null || pending.QuestionId != request.QuestionId)
            {
                throw new InvalidOperationException("复习题目已经过期、不属于当前会话或会话已完成。");
            }
            var session = ReviewSessionMapper.ToDto(snapshot.Session);
            var unit = snapshot.Units.Single(value => value.MemberId == pending.MemberId && value.FormCode == pending.FormCode);
            var output = Enum.Parse<LearningOutputMode>(pending.OutputMode);
            var wasCorrect = evaluation.IsCorrect(request.SubmittedText,
                PracticeQuestionPresenter.GetAnswers(unit, output, session.Language), session.Language, cancellationToken);
            var targets = PracticeDictationTargets.Select(unit, pending, snapshot.Units, session.Language, session.PlanSnapshot);
            var history = snapshot.History.Where(value => value.QuestionId != pending.QuestionId)
                .Concat(targets.Select(target => new PracticeQuestionHistoryPo(pending.QuestionId, target.WordGroupId, target.MemberId,
                    target.FormCode, pending.Channel, pending.OutputMode, pending.QuestionOrdinal, wasCorrect))).ToArray();
            var nextEligible = scheduler.GetNextEligibleOrdinal(pending.QuestionOrdinal);
            var changed = targets.Select(target => PracticeAnswerTransition.Apply(target, pending, wasCorrect,
                wasCorrect ? Math.Min(2, target.CorrectStreak + 1) : 0, nextEligible,
                scheduler.HasUnresolvedRetry(target, session.PlanSnapshot, session.Language, history))).ToArray();
            var byIdentity = changed.ToDictionary(value => (value.MemberId, value.FormCode));
            var updatedUnits = snapshot.Units.Select(value => byIdentity.GetValueOrDefault((value.MemberId, value.FormCode)) ?? value).ToArray();
            var projected = snapshot with { CurrentOrdinal = pending.QuestionOrdinal + 1, PendingQuestion = null, Units = updatedUnits, History = history };
            var changedGroups = targets.Select(target => target.WordGroupId).ToHashSet(StringComparer.Ordinal);
            var completedGroups = updatedUnits.Where(value => changedGroups.Contains(value.WordGroupId)).GroupBy(value => value.WordGroupId)
                .Where(group => group.All(value => value.IsMastered)).Select(group => group.Key).ToArray();
            var completeSession = updatedUnits.All(value => value.IsMastered);
            var nextScheduled = completeSession ? null : scheduler.CreateNextQuestion(session.SessionId, session.Language,
                updatedUnits, history, session.PlanSnapshot, projected.CurrentOrdinal);
            if (!completeSession && nextScheduled is null)
            {
                throw new InvalidOperationException("复习尚未完成，但没有可调度的下一题。");
            }
            var now = timeProvider.GetUtcNow();
            var memories = memoryPlanner.CreateUpdates(changed.Where(value => value.IsMastered).ToArray(), snapshot.Memories, now);
            var next = await answers.ApplyAsync(new ApplyReviewAnswerPo(session.SessionId, pending.QuestionId,
                (request.SubmittedText ?? string.Empty).Trim().Normalize(NormalizationForm.FormC), wasCorrect,
                changed.Select(ReviewAnswerMapper.ToOutcome).ToArray(), now,
                timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), nextScheduled, completedGroups, completeSession, memories), cancellationToken);
            return new ReviewAnswerResultDto(wasCorrect, PracticeQuestionPresenter.SelectPreferredAnswer(unit, output, session.Language),
                PracticeQuestionPresenter.SelectPreferredSpokenText(unit, output, session.Language), history.Any(value => value.WasCorrect == false),
                next is null ? null : PracticeQuestionPresenter.Create(session.Language, updatedUnits, next, session.PlanSnapshot),
                completeSession ? ReviewAnswerMapper.ToSummary(projected, session.Language) : null);
        }
    }
}
