using System.Globalization;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Dictation.Services.Lexicon;

namespace Dictation.Services.Learning
{
    public sealed class LearningSessionService
    {
        private readonly LearningSessionDataAccessor dataAccessor;
        private readonly LearningPlanService learningPlanService;
        private readonly AnswerEvaluationService answerEvaluationService;
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        public LearningSessionService(
            LearningSessionDataAccessor dataAccessor,
            LearningPlanService learningPlanService,
            AnswerEvaluationService answerEvaluationService,
            TimeProvider timeProvider,
            Random random)
        {
            ArgumentNullException.ThrowIfNull(dataAccessor);
            ArgumentNullException.ThrowIfNull(learningPlanService);
            ArgumentNullException.ThrowIfNull(answerEvaluationService);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(random);
            this.dataAccessor = dataAccessor;
            this.learningPlanService = learningPlanService;
            this.answerEvaluationService = answerEvaluationService;
            this.timeProvider = timeProvider;
            this.random = random;
        }

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
                return MapSession(active);
            }

            var plan = await learningPlanService.LoadAsync(request.Language, cancellationToken);
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
            return MapSession(created);
        }

        public async Task<LearningSessionDto?> GetActiveAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var session = await dataAccessor.GetActiveAsync(language.ToString(), cancellationToken);
            return session is null ? null : MapSession(session);
        }

        public async Task<IReadOnlyList<LearningBrowseItemDto>> GetBrowseItemsAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var items = await dataAccessor.GetBrowseItemsAsync(sessionId.Trim(), cancellationToken);
            return items.Select(item => new LearningBrowseItemDto(
                item.SortOrder,
                item.WordGroupId,
                item.Members.Select(MapMember).ToArray())).ToArray();
        }

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

            var plan = await learningPlanService.LoadAsync(language, cancellationToken);
            if (snapshot.PendingQuestion is not null)
            {
                return MapQuestion(snapshot, snapshot.PendingQuestion, plan);
            }

            var scheduled = ScheduleQuestion(snapshot, plan, snapshot.Session.CurrentOrdinal)
                ?? throw new InvalidOperationException("当前学习会话没有可调度的题目。");
            var persisted = await dataAccessor.CreatePendingQuestionAsync(scheduled.Question, cancellationToken);
            return MapQuestion(snapshot, persisted, plan);
        }

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
            var plan = await learningPlanService.LoadAsync(language, cancellationToken);
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
            var nextEligibleOrdinal = currentQuestion.QuestionOrdinal + random.Next(4, 9);
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
            var activeUnits = updatedUnits.Where(unit => IsEnabledUnit(unit, plan, language)).ToArray();
            var completedWordGroupIds = activeUnits
                .GroupBy(unit => unit.WordGroupId, StringComparer.Ordinal)
                .Where(group => group.All(unit => unit.IsMastered))
                .Select(group => group.Key)
                .ToArray();
            var completeSession = activeUnits.Length == 0 || activeUnits.All(unit => unit.IsMastered);
            var nextScheduled = completeSession ? null : ScheduleQuestion(transitionedSnapshot, plan, nextOrdinal);
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
                    nextScheduled?.Question,
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
                summary);
        }

        private ScheduledQuestion? ScheduleQuestion(
            LearningSchedulingSnapshotPo snapshot,
            LearningPlanDto plan,
            int questionOrdinal)
        {
            var language = ParseEnum<LearningLanguage>(snapshot.Session.Language, "学习语言");
            var candidates = snapshot.Units
                .Where(unit => !unit.IsMastered && IsEnabledUnit(unit, plan, language))
                .Select(unit => CreateQuestionCandidate(unit, plan, language, snapshot.History))
                .Where(candidate => candidate is not null)
                .Cast<QuestionCandidate>()
                .ToArray();
            var eligible = candidates.Where(candidate => candidate.Unit.NextEligibleOrdinal <= questionOrdinal).ToArray();
            if (eligible.Length == 0)
            {
                eligible = candidates.Where(candidate => MayReleaseEarly(candidate, candidates, snapshot.History)).ToArray();
            }

            if (eligible.Length == 0)
            {
                return null;
            }

            var maxUncovered = eligible.Max(candidate => candidate.UncoveredDimensions);
            var coveragePool = eligible.Where(candidate => candidate.UncoveredDimensions == maxUncovered).ToArray();
            var maxErrorRate = coveragePool.Max(candidate => candidate.ErrorRate);
            var priorityPool = coveragePool
                .Where(candidate => Math.Abs(candidate.ErrorRate - maxErrorRate) < double.Epsilon)
                .ToArray();
            var selected = priorityPool[random.Next(priorityPool.Length)];
            return new ScheduledQuestion(new CreateLearningQuestionPo(
                snapshot.Session.SessionId,
                selected.Unit.WordGroupId,
                selected.Unit.MemberId,
                selected.Unit.FormCode,
                selected.Channel.ToString(),
                selected.Output.ToString(),
                questionOrdinal,
                timeProvider.GetUtcNow()));
        }

        private QuestionCandidate? CreateQuestionCandidate(
            LearningFormUnitPo unit,
            LearningPlanDto plan,
            LearningLanguage language,
            IReadOnlyList<LearningQuestionHistoryPo> history)
        {
            var outputs = GetApplicableOutputs(unit, plan, language);
            if (outputs.Count == 0)
            {
                return null;
            }

            var channel = SelectWeakest(
                plan.Channels,
                value => value == LearningChannel.MeaningWriting ? unit.MeaningWritingCount : unit.DictationCount);
            var output = SelectWeakest(outputs, value => GetOutputCount(unit, value));
            var uncovered = (GetChannelCount(unit, channel) == 0 ? 1 : 0)
                + (GetOutputCount(unit, output) == 0 ? 1 : 0);
            var totalAnswers = unit.CorrectCount + unit.ErrorCount;
            var errorRate = totalAnswers == 0 ? 0d : (double)unit.ErrorCount / totalAnswers;
            var lastOrdinal = history
                .Where(item => IsSameUnit(unit, item))
                .Select(item => item.QuestionOrdinal)
                .DefaultIfEmpty(-1)
                .Max();
            return new QuestionCandidate(unit, channel, output, uncovered, errorRate, lastOrdinal);
        }

        private T SelectWeakest<T>(IReadOnlyList<T> values, Func<T, int> countSelector) where T : struct, Enum
        {
            var minimum = values.Min(countSelector);
            var pool = values.Where(value => countSelector(value) == minimum).ToArray();
            return pool[random.Next(pool.Length)];
        }

        private static bool MayReleaseEarly(
            QuestionCandidate candidate,
            IReadOnlyList<QuestionCandidate> candidates,
            IReadOnlyList<LearningQuestionHistoryPo> history)
        {
            return candidates
                .Where(other => !IsSameUnit(candidate.Unit, other.Unit))
                .All(other => history.Any(item =>
                    item.WasCorrect.HasValue
                    && item.QuestionOrdinal > candidate.LastQuestionOrdinal
                    && IsSameUnit(other.Unit, item)));
        }

        private static IReadOnlyList<LearningOutputMode> GetApplicableOutputs(
            LearningFormUnitPo unit,
            LearningPlanDto plan,
            LearningLanguage language)
        {
            return plan.OutputModes.Where(output =>
            {
                if (language == LearningLanguage.English)
                {
                    return output == LearningOutputMode.Latin && unit.Variants.Count > 0;
                }

                return output switch
                {
                    LearningOutputMode.Kana => unit.Variants.Any(variant => string.Equals(variant.ScriptCode, "Kana", StringComparison.Ordinal)),
                    LearningOutputMode.Kanji => string.Equals(unit.KanjiUsageCode, "Common", StringComparison.Ordinal)
                        && unit.Variants.Any(variant => string.Equals(variant.ScriptCode, "Kanji", StringComparison.Ordinal)),
                    _ => false
                };
            }).ToArray();
        }

        private static bool IsApplicableForm(LearningFormUnitPo unit, LearningLanguage language)
        {
            if (!unit.IsPhrase)
            {
                return true;
            }

            return language == LearningLanguage.English
                ? string.Equals(unit.FormCode, "Base", StringComparison.Ordinal)
                : string.Equals(unit.FormCode, "Dictionary", StringComparison.Ordinal);
        }

        private static bool IsEnabledUnit(
            LearningFormUnitPo unit,
            LearningPlanDto plan,
            LearningLanguage language) =>
            plan.FormCodes.Contains(unit.FormCode, StringComparer.Ordinal)
            && IsApplicableForm(unit, language)
            && GetApplicableOutputs(unit, plan, language).Count > 0;

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
                channel == LearningChannel.Dictation ? SelectPreferredAnswer(unit, output, language) : null);
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

        private static LearningFormUnitPo FindUnit(
            IReadOnlyList<LearningFormUnitPo> units,
            LearningQuestionStatePo question)
        {
            return units.SingleOrDefault(unit => IsSameUnit(unit, question))
                ?? throw new InvalidDataException("学习题目对应的词形状态不存在。");
        }

        private static int GetChannelCount(LearningFormUnitPo unit, LearningChannel channel) => channel switch
        {
            LearningChannel.MeaningWriting => unit.MeaningWritingCount,
            LearningChannel.Dictation => unit.DictationCount,
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        private static int GetOutputCount(LearningFormUnitPo unit, LearningOutputMode output) => output switch
        {
            LearningOutputMode.Latin => unit.LatinCount,
            LearningOutputMode.Kana => unit.KanaCount,
            LearningOutputMode.Kanji => unit.KanjiCount,
            _ => throw new ArgumentOutOfRangeException(nameof(output))
        };

        private static bool IsSameUnit(LearningFormUnitPo left, LearningFormUnitPo right) =>
            left.MemberId == right.MemberId && string.Equals(left.FormCode, right.FormCode, StringComparison.Ordinal);

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

        private sealed record ScheduledQuestion(CreateLearningQuestionPo Question);

        private sealed record QuestionCandidate(
            LearningFormUnitPo Unit,
            LearningChannel Channel,
            LearningOutputMode Output,
            int UncoveredDimensions,
            double ErrorRate,
            int LastQuestionOrdinal);

        private async Task<IReadOnlyList<EligibleCandidate>> SelectManualAsync(
            StartLearningSessionRequestDto request,
            LearningPlanDto plan,
            IReadOnlyList<EligibleCandidate> eligible,
            int remainingNewCount,
            CancellationToken cancellationToken)
        {
            var startRequest = await learningPlanService.CreateStartRequestAsync(
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

        private static LearningSessionDto MapSession(LearningSessionPo session)
        {
            return new LearningSessionDto(
                session.SessionId,
                ParseEnum<LearningLanguage>(session.Language, "学习语言"),
                ParseEnum<LearningSessionStatus>(session.Status, "学习会话状态"),
                session.CurrentOrdinal,
                session.CreatedAtUtc,
                session.CompletedAtUtc,
                session.WordGroupIds);
        }

        private static ImportWordGroupMemberDto MapMember(ImportWordGroupMemberPo member)
        {
            return new ImportWordGroupMemberDto(
                member.Language,
                member.SourceSenseId,
                member.SourceLexemeId,
                member.Headword,
                member.Reading,
                member.PartOfSpeechLabel,
                member.AttributeLabel,
                member.IsPhrase,
                string.Equals(member.MeaningLanguage, "zh-CN", StringComparison.Ordinal)
                    ? member.Meanings
                    : Array.Empty<string>(),
                member.Forms.Select(form => new ImportWordGroupFormDto(
                    form.Surface,
                    form.Reading,
                    form.FormCode,
                    form.ScriptCode,
                    form.RoleCode,
                    form.FormLabel,
                    form.DetailLabel,
                    form.IsCommon,
                    form.IsPrimary)).ToArray(),
                ParseEnumOrDefault(member.MeaningSource, LexiconMeaningSource.Missing),
                ParseEnumOrDefault(member.MeaningPrecision, LexiconMeaningPrecision.Missing),
                member.KanjiUsageCode);
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

        private static T ParseEnumOrDefault<T>(string value, T defaultValue) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : defaultValue;
        }

        private sealed record EligibleCandidate(
            LearningSessionCandidatePo Candidate,
            IReadOnlyList<LearningFormIdentityPo> Forms);
    }
}
