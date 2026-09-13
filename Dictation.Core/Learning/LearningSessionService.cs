using System.Globalization;
using Dictation.DataAccess.Learning;
using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 编排学习会话的启动、浏览、出题、答题与完成持久化流程。
    /// </summary>
    internal sealed class LearningSessionService : ILearningSessionOperations
    {
        private readonly LearningSessionDataAccessor dataAccessor;
        private readonly ILearningPlanReader learningPlanReader;
        private readonly ILearningSessionStartPreparer sessionStartPreparer;
        private readonly LearningQuestionScheduler questionScheduler;
        private readonly AnswerEvaluationService answerEvaluationService;
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        /// <summary>
        /// 初始化学习会话编排服务。
        /// </summary>
        /// <param name="dataAccessor">用于读写会话、题目和学习进度的数据访问器。</param>
        /// <param name="learningPlanReader">用于按会话语言读取当前学习计划。</param>
        /// <param name="sessionStartPreparer">用于校验手选词组并准备启动范围。</param>
        /// <param name="questionScheduler">用于按当前词形状态和历史选择下一题。</param>
        /// <param name="answerEvaluationService">用于依据语言规则判断提交答案。</param>
        /// <param name="timeProvider">用于记录会话、题目和答案的时间。</param>
        /// <param name="random">用于保留选词与出题的随机顺序。</param>
        public LearningSessionService(
            LearningSessionDataAccessor dataAccessor,
            ILearningPlanReader learningPlanReader,
            ILearningSessionStartPreparer sessionStartPreparer,
            LearningQuestionScheduler questionScheduler,
            AnswerEvaluationService answerEvaluationService,
            TimeProvider timeProvider,
            Random random)
        {
            ArgumentNullException.ThrowIfNull(dataAccessor);
            ArgumentNullException.ThrowIfNull(learningPlanReader);
            ArgumentNullException.ThrowIfNull(sessionStartPreparer);
            ArgumentNullException.ThrowIfNull(questionScheduler);
            ArgumentNullException.ThrowIfNull(answerEvaluationService);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(random);
            this.dataAccessor = dataAccessor;
            this.learningPlanReader = learningPlanReader;
            this.sessionStartPreparer = sessionStartPreparer;
            this.questionScheduler = questionScheduler;
            this.answerEvaluationService = answerEvaluationService;
            this.timeProvider = timeProvider;
            this.random = random;
        }

        /// <inheritdoc />
        public async Task<LearningSessionDto> StartOrResumeAsync(
            StartLearningSessionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLanguage(request.Language);
            cancellationToken.ThrowIfCancellationRequested();

            var active = await dataAccessor.GetActiveAsync(request.Language.ToString(), cancellationToken);
            if (active is not null)
            {
                return LearningSessionMapper.ToDto(active);
            }

            var plan = await learningPlanReader.LoadAsync(request.Language, cancellationToken);
            var candidates = await dataAccessor.GetCandidatesAsync(request.Language.ToString(), cancellationToken);
            var enabledForms = plan.FormCodes.ToHashSet(StringComparer.Ordinal);
            var eligible = candidates
                .Select(candidate => CreateEligibleCandidate(
                    candidate,
                    enabledForms,
                    plan.OutputModes,
                    request.Language))
                .Where(candidate => candidate is not null)
                .Cast<EligibleCandidate>()
                .ToArray();
            var localDate = timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var startedToday = await dataAccessor.CountStartedOnDateAsync(
                request.Language.ToString(),
                localDate,
                cancellationToken);
            var remainingNewCount = Math.Max(0, plan.DailyNewTarget - startedToday);
            var selected = plan.SelectionMode == LearningSelectionMode.Manual
                ? await SelectManualAsync(request, plan, eligible, remainingNewCount, cancellationToken)
                : SelectAutomatic(plan, eligible, remainingNewCount);
            var now = timeProvider.GetUtcNow();
            var status = selected.Count == 0 ? LearningSessionStatus.Completed : LearningSessionStatus.InProgress;
            var created = await dataAccessor.CreateAsync(
                new CreateLearningSessionPo(
                    Guid.NewGuid().ToString("N"),
                    request.Language.ToString(),
                    status.ToString(),
                    localDate,
                    now,
                    status == LearningSessionStatus.Completed ? now : null,
                    selected.Select(candidate => new LearningSessionGroupSeedPo(
                        candidate.Candidate.WordGroupId,
                        candidate.Forms)).ToArray()),
                cancellationToken);
            return LearningSessionMapper.ToDto(created);
        }

        /// <summary>
        /// 读取指定语言尚未完成的会话。
        /// </summary>
        /// <param name="language">要查询的学习语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>同语言活动会话；不存在时为 <see langword="null"/>。</returns>
        public async Task<LearningSessionDto?> GetActiveAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var session = await dataAccessor.GetActiveAsync(language.ToString(), cancellationToken);
            return session is null ? null : LearningSessionMapper.ToDto(session);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LearningBrowseItemDto>> GetBrowseItemsAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var items = await dataAccessor.GetBrowseItemsAsync(sessionId.Trim(), cancellationToken);
            return items.Select(LearningSessionMapper.ToDto).ToArray();
        }

        /// <inheritdoc />
        public async Task<LearningQuestionDto?> GetCurrentQuestionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await dataAccessor.GetSchedulingSnapshotAsync(sessionId.Trim(), cancellationToken);
            var language = ParseEnum<LearningLanguage>(snapshot.Session.Language, "学习语言");
            if (string.Equals(snapshot.Session.Status, LearningSessionStatus.Completed.ToString(), StringComparison.Ordinal))
            {
                return null;
            }

            var plan = await learningPlanReader.LoadAsync(language, cancellationToken);
            if (snapshot.PendingQuestion is not null)
            {
                return MapQuestion(snapshot, snapshot.PendingQuestion, plan);
            }

            var scheduled = questionScheduler.CreateNextQuestion(snapshot, plan, snapshot.Session.CurrentOrdinal)
                ?? throw new InvalidOperationException("当前学习会话没有可调度的题目。");
            var persisted = await dataAccessor.CreatePendingQuestionAsync(scheduled, cancellationToken);
            return MapQuestion(snapshot, persisted, plan);
        }

        /// <inheritdoc />
        public async Task<LearningAnswerResultDto> SubmitAnswerAsync(
            SubmitLearningAnswerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var sessionId = request.SessionId.Trim();
            var snapshot = await dataAccessor.GetSchedulingSnapshotAsync(sessionId, cancellationToken);
            if (!string.Equals(snapshot.Session.Status, LearningSessionStatus.InProgress.ToString(), StringComparison.Ordinal)
                || snapshot.PendingQuestion is null
                || snapshot.PendingQuestion.QuestionId != request.QuestionId)
            {
                throw new InvalidOperationException("题目已过期、不属于当前会话或会话已完成。");
            }

            var language = ParseEnum<LearningLanguage>(snapshot.Session.Language, "学习语言");
            var plan = await learningPlanReader.LoadAsync(language, cancellationToken);
            var currentQuestion = snapshot.PendingQuestion;
            var currentUnit = FindUnit(snapshot.Units, currentQuestion);
            var output = ParseEnum<LearningOutputMode>(currentQuestion.OutputMode, "输出方式");
            var answers = GetAnswers(currentUnit, output, language);
            var wasCorrect = answerEvaluationService.IsCorrect(
                request.SubmittedText,
                answers,
                language,
                cancellationToken);
            var submittedText = (request.SubmittedText ?? string.Empty)
                .Trim()
                .Normalize(System.Text.NormalizationForm.FormC);
            var newStreak = wasCorrect ? Math.Min(2, currentUnit.CorrectStreak + 1) : 0;
            var nextEligibleOrdinal = questionScheduler.GetNextEligibleOrdinal(currentQuestion.QuestionOrdinal);
            var updatedUnit = ApplyOutcome(currentUnit, currentQuestion, wasCorrect, newStreak, nextEligibleOrdinal);
            var updatedUnits = snapshot.Units
                .Select(unit => IsSameUnit(unit, currentQuestion) ? updatedUnit : unit)
                .ToArray();
            var updatedHistory = snapshot.History
                .Select(history => history.QuestionId == currentQuestion.QuestionId
                    ? history with { WasCorrect = wasCorrect }
                    : history)
                .ToArray();
            var nextOrdinal = currentQuestion.QuestionOrdinal + 1;
            var transitionedSnapshot = snapshot with
            {
                Session = snapshot.Session with { CurrentOrdinal = nextOrdinal },
                PendingQuestion = null,
                Units = updatedUnits,
                History = updatedHistory
            };
            var activeUnits = updatedUnits
                .Where(unit => questionScheduler.IsEnabledUnit(unit, plan, language))
                .ToArray();
            var completedWordGroupIds = activeUnits
                .GroupBy(unit => unit.WordGroupId, StringComparer.Ordinal)
                .Where(group => group.All(unit => unit.IsMastered))
                .Select(group => group.Key)
                .ToArray();
            var completeSession = activeUnits.Length == 0 || activeUnits.All(unit => unit.IsMastered);
            var nextScheduled = completeSession
                ? null
                : questionScheduler.CreateNextQuestion(transitionedSnapshot, plan, nextOrdinal);
            if (!completeSession && nextScheduled is null)
            {
                throw new InvalidOperationException("学习会话尚未完成，但没有可调度的下一题。");
            }

            var now = timeProvider.GetUtcNow();
            var localDate = timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var persistedNext = await dataAccessor.ApplyAnswerAsync(
                new ApplyLearningAnswerPo(
                    sessionId,
                    currentQuestion.QuestionId,
                    submittedText,
                    wasCorrect,
                    newStreak,
                    updatedUnit.IsMastered,
                    nextEligibleOrdinal,
                    now,
                    localDate,
                    nextScheduled,
                    completedWordGroupIds,
                    completeSession),
                cancellationToken);
            var hadError = updatedHistory.Any(history => history.WasCorrect == false);
            var nextQuestion = persistedNext is null ? null : MapQuestion(transitionedSnapshot, persistedNext, plan);
            var summary = completeSession
                ? CreateSummary(sessionId, transitionedSnapshot, language)
                : null;
            return new LearningAnswerResultDto(
                wasCorrect,
                SelectPreferredAnswer(currentUnit, output, language),
                hadError,
                nextQuestion,
                summary,
                SelectPreferredSpokenText(currentUnit, output, language));
        }

        private static LearningFormUnitPo ApplyOutcome(
            LearningFormUnitPo unit,
            LearningQuestionStatePo question,
            bool wasCorrect,
            int correctStreak,
            int nextEligibleOrdinal)
        {
            return unit with
            {
                CorrectStreak = correctStreak,
                IsMastered = correctStreak == 2,
                NextEligibleOrdinal = nextEligibleOrdinal,
                MeaningWritingCount = unit.MeaningWritingCount + (question.Channel == LearningChannel.MeaningWriting.ToString() ? 1 : 0),
                DictationCount = unit.DictationCount + (question.Channel == LearningChannel.Dictation.ToString() ? 1 : 0),
                LatinCount = unit.LatinCount + (question.OutputMode == LearningOutputMode.Latin.ToString() ? 1 : 0),
                KanaCount = unit.KanaCount + (question.OutputMode == LearningOutputMode.Kana.ToString() ? 1 : 0),
                KanjiCount = unit.KanjiCount + (question.OutputMode == LearningOutputMode.Kanji.ToString() ? 1 : 0),
                CorrectCount = unit.CorrectCount + (wasCorrect ? 1 : 0),
                ErrorCount = unit.ErrorCount + (wasCorrect ? 0 : 1)
            };
        }

        private static LearningQuestionDto MapQuestion(
            LearningSchedulingSnapshotPo snapshot,
            LearningQuestionStatePo question,
            LearningPlanDto plan)
        {
            var language = ParseEnum<LearningLanguage>(snapshot.Session.Language, "学习语言");
            var channel = ParseEnum<LearningChannel>(question.Channel, "题型");
            var output = ParseEnum<LearningOutputMode>(question.OutputMode, "输出方式");
            var unit = FindUnit(snapshot.Units, question);
            return new LearningQuestionDto(
                question.QuestionId,
                question.SessionId,
                question.QuestionOrdinal,
                question.WordGroupId,
                language,
                channel,
                output,
                question.FormCode,
                GetFormLabel(unit, plan, language),
                GetOutputLabel(output),
                unit.Meanings,
                channel == LearningChannel.Dictation ? SelectPreferredSpokenText(unit, output, language) : null);
        }

        private static string GetFormLabel(
            LearningFormUnitPo unit,
            LearningPlanDto plan,
            LearningLanguage language)
        {
            if (language == LearningLanguage.English
                && plan.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                && plan.EnglishExampleParadigm is not null)
            {
                var example = plan.EnglishExampleParadigm.Forms.FirstOrDefault(form =>
                    string.Equals(form.FormCode, unit.FormCode, StringComparison.Ordinal));
                if (example is not null)
                {
                    return example.Surface;
                }
            }

            return unit.FormLabel;
        }

        private static string GetOutputLabel(LearningOutputMode output) => output switch
        {
            LearningOutputMode.Latin => "英文",
            LearningOutputMode.Kana => "假名",
            LearningOutputMode.Kanji => "汉字",
            _ => throw new ArgumentOutOfRangeException(nameof(output))
        };

        private static IReadOnlyList<string> GetAnswers(
            LearningFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language)
        {
            var answers = unit.Variants
                .Where(variant => language == LearningLanguage.English
                    ? output == LearningOutputMode.Latin
                    : string.Equals(variant.ScriptCode, output.ToString(), StringComparison.Ordinal))
                .Select(variant => variant.Surface)
                .Distinct(language == LearningLanguage.English ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .ToArray();
            return answers.Length > 0
                ? answers
                : throw new InvalidDataException("学习题目没有可接受答案。");
        }

        private static string SelectPreferredAnswer(
            LearningFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language) => GetAnswers(unit, output, language)[0];

        private static string SelectPreferredSpokenText(
            LearningFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language)
        {
            var answer = SelectPreferredAnswer(unit, output, language);
            if (language == LearningLanguage.English)
            {
                return answer;
            }

            var variant = unit.Variants.First(value => value.Surface == answer
                && string.Equals(value.ScriptCode, output.ToString(), StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(variant.Reading) ? answer : variant.Reading;
        }

        private static LearningFormUnitPo FindUnit(
            IReadOnlyList<LearningFormUnitPo> units,
            LearningQuestionStatePo question)
        {
            return units.SingleOrDefault(unit => IsSameUnit(unit, question))
                ?? throw new InvalidDataException("学习题目对应的词形状态不存在。");
        }

        private static bool IsSameUnit(LearningFormUnitPo unit, LearningQuestionStatePo question) =>
            unit.MemberId == question.MemberId && string.Equals(unit.FormCode, question.FormCode, StringComparison.Ordinal);

        private static bool IsSameUnit(LearningFormUnitPo unit, LearningQuestionHistoryPo question) =>
            unit.MemberId == question.MemberId && string.Equals(unit.FormCode, question.FormCode, StringComparison.Ordinal);

        private static LearningSessionSummaryDto CreateSummary(
            string sessionId,
            LearningSchedulingSnapshotPo snapshot,
            LearningLanguage language)
        {
            var errors = snapshot.History
                .Where(history => history.WasCorrect == false)
                .DistinctBy(history => (history.WordGroupId, history.MemberId, history.FormCode))
                .Select(history =>
                {
                    var unit = snapshot.Units.Single(value => IsSameUnit(value, history));
                    var output = ParseEnum<LearningOutputMode>(history.OutputMode, "输出方式");
                    return new LearningErrorSummaryDto(
                        history.WordGroupId,
                        history.FormCode,
                        SelectPreferredAnswer(unit, output, language));
                })
                .ToArray();
            return new LearningSessionSummaryDto(
                sessionId,
                snapshot.History.Count(history => history.WasCorrect.HasValue),
                errors);
        }

        private async Task<IReadOnlyList<EligibleCandidate>> SelectManualAsync(
            StartLearningSessionRequestDto request,
            LearningPlanDto plan,
            IReadOnlyList<EligibleCandidate> eligible,
            int remainingNewCount,
            CancellationToken cancellationToken)
        {
            var startRequest = await sessionStartPreparer.CreateStartRequestAsync(
                request.Language,
                request.SelectedWordGroupIds,
                cancellationToken);
            var byId = eligible.ToDictionary(candidate => candidate.Candidate.WordGroupId, StringComparer.Ordinal);
            var selected = new List<EligibleCandidate>();
            var selectedNewCount = 0;
            foreach (var wordGroupId in startRequest.SelectedWordGroupIds)
            {
                if (!byId.TryGetValue(wordGroupId, out var candidate))
                {
                    throw new ArgumentException("选择中包含当前计划没有可练习词形的词组。", nameof(request.SelectedWordGroupIds));
                }

                if (!candidate.Candidate.HasProgress)
                {
                    if (selectedNewCount >= remainingNewCount)
                    {
                        continue;
                    }

                    selectedNewCount++;
                }

                selected.Add(candidate);
                if (selected.Count == plan.SessionWordLimit)
                {
                    break;
                }
            }

            return selected;
        }

        private IReadOnlyList<EligibleCandidate> SelectAutomatic(
            LearningPlanDto plan,
            IReadOnlyList<EligibleCandidate> eligible,
            int remainingNewCount)
        {
            var historical = OrderCandidates(
                eligible.Where(candidate => candidate.Candidate.HasProgress),
                plan.SelectionMode);
            var newCandidates = OrderCandidates(
                    eligible.Where(candidate => !candidate.Candidate.HasProgress),
                    plan.SelectionMode)
                .Take(remainingNewCount);
            return historical.Concat(newCandidates).Take(plan.SessionWordLimit).ToArray();
        }

        private IReadOnlyList<EligibleCandidate> OrderCandidates(
            IEnumerable<EligibleCandidate> candidates,
            LearningSelectionMode selectionMode)
        {
            var ordered = candidates
                .OrderBy(candidate => candidate.Candidate.CreatedAtUtc)
                .ThenBy(candidate => candidate.Candidate.WordGroupId, StringComparer.Ordinal)
                .ToArray();
            if (selectionMode == LearningSelectionMode.Random)
            {
                for (var index = ordered.Length - 1; index > 0; index--)
                {
                    var swapIndex = random.Next(index + 1);
                    (ordered[index], ordered[swapIndex]) = (ordered[swapIndex], ordered[index]);
                }
            }

            return ordered;
        }

        private static EligibleCandidate? CreateEligibleCandidate(
            LearningSessionCandidatePo candidate,
            IReadOnlySet<string> enabledForms,
            IReadOnlyList<LearningOutputMode> outputModes,
            LearningLanguage language)
        {
            var forms = candidate.AvailableForms
                .Where(form => enabledForms.Contains(form.FormCode))
                .Where(form => IsApplicableCandidateForm(form, outputModes, language))
                .DistinctBy(form => (form.MemberId, form.FormCode))
                .ToArray();
            if (forms.Length == 0)
            {
                return null;
            }

            var states = candidate.FormStates.ToDictionary(
                state => (state.MemberId, state.FormCode),
                state => state.IsMastered);
            var requiresPractice = !candidate.HasProgress
                || forms.Any(form => !states.TryGetValue((form.MemberId, form.FormCode), out var mastered) || !mastered);
            return requiresPractice ? new EligibleCandidate(candidate, forms) : null;
        }

        private static bool IsApplicableCandidateForm(
            LearningFormIdentityPo form,
            IReadOnlyList<LearningOutputMode> outputModes,
            LearningLanguage language)
        {
            if (language == LearningLanguage.English)
            {
                return outputModes.Contains(LearningOutputMode.Latin);
            }

            return outputModes.Any(output => output switch
            {
                LearningOutputMode.Kana => string.Equals(form.ScriptCode, "Kana", StringComparison.Ordinal),
                LearningOutputMode.Kanji => string.Equals(form.KanjiUsageCode, "Common", StringComparison.Ordinal)
                    && string.Equals(form.ScriptCode, "Kanji", StringComparison.Ordinal),
                _ => false
            });
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言不受支持。");
            }
        }

        private static T ParseEnum<T>(string value, string fieldName) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException($"数据库中的{fieldName}无效：{value}。");
        }

        private sealed record EligibleCandidate(
            LearningSessionCandidatePo Candidate,
            IReadOnlyList<LearningFormIdentityPo> Forms);
    }
}
