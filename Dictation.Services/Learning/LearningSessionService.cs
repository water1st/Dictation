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
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        public LearningSessionService(
            LearningSessionDataAccessor dataAccessor,
            LearningPlanService learningPlanService,
            TimeProvider timeProvider,
            Random random)
        {
            ArgumentNullException.ThrowIfNull(dataAccessor);
            ArgumentNullException.ThrowIfNull(learningPlanService);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(random);
            this.dataAccessor = dataAccessor;
            this.learningPlanService = learningPlanService;
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
                .Select(candidate => CreateEligibleCandidate(candidate, enabledForms))
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
            IReadOnlySet<string> enabledForms)
        {
            var forms = candidate.AvailableForms
                .Where(form => enabledForms.Contains(form.FormCode))
                .Distinct()
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
