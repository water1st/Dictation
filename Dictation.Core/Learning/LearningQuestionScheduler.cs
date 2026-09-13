using Dictation.Core.Configuration;
using Dictation.DataAccess.Learning;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 依据学习计划、词形状态与答题历史执行纯出题调度决策。
    /// </summary>
    internal sealed class LearningQuestionScheduler
    {
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        /// <summary>
        /// 初始化出题调度组件，并复用应用级时钟与随机源。
        /// </summary>
        /// <param name="timeProvider">用于记录新题创建时间的时钟。</param>
        /// <param name="random">用于候选选择与重练间隔的随机源。</param>
        public LearningQuestionScheduler(TimeProvider timeProvider, Random random)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// 按 4–8 个题号偏移计算当前词形下一次可重练的序号。
        /// </summary>
        /// <param name="questionOrdinal">刚完成题目的序号。</param>
        /// <returns>该词形下一次常规可调度的题目序号。</returns>
        internal int GetNextEligibleOrdinal(int questionOrdinal)
        {
            return questionOrdinal + random.Next(4, 9);
        }

        /// <summary>
        /// 从当前会话快照中选择下一题；没有可调度词形时返回空。
        /// </summary>
        /// <param name="snapshot">包含词形状态、历史和会话语言的持久化快照。</param>
        /// <param name="plan">当前语言生效的学习计划。</param>
        /// <param name="questionOrdinal">待创建题目的序号。</param>
        /// <returns>下一题持久化输入；无候选时为 <see langword="null"/>。</returns>
        internal CreateLearningQuestionPo? CreateNextQuestion(
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
            var eligible = candidates
                .Where(candidate => candidate.Unit.NextEligibleOrdinal <= questionOrdinal)
                .ToArray();
            if (eligible.Length == 0)
            {
                eligible = candidates
                    .Where(candidate => MayReleaseEarly(candidate, candidates, snapshot.History))
                    .ToArray();
            }

            if (eligible.Length == 0)
            {
                return null;
            }

            var maxUncovered = eligible.Max(candidate => candidate.UncoveredDimensions);
            var coveragePool = eligible
                .Where(candidate => candidate.UncoveredDimensions == maxUncovered)
                .ToArray();
            var maxErrorRate = coveragePool.Max(candidate => candidate.ErrorRate);
            var priorityPool = coveragePool
                .Where(candidate => Math.Abs(candidate.ErrorRate - maxErrorRate) < double.Epsilon)
                .ToArray();
            var selected = priorityPool[random.Next(priorityPool.Length)];
            return new CreateLearningQuestionPo(
                snapshot.Session.SessionId,
                selected.Unit.WordGroupId,
                selected.Unit.MemberId,
                selected.Unit.FormCode,
                selected.Channel.ToString(),
                selected.Output.ToString(),
                questionOrdinal,
                timeProvider.GetUtcNow());
        }

        /// <summary>
        /// 判断一个词形是否仍在当前计划和语言字形范围内可练习。
        /// </summary>
        /// <param name="unit">待判断的词形状态。</param>
        /// <param name="plan">当前语言生效的学习计划。</param>
        /// <param name="language">当前会话语言。</param>
        /// <returns>词形在当前计划中可练习时返回 <see langword="true"/>。</returns>
        internal bool IsEnabledUnit(
            LearningFormUnitPo unit,
            LearningPlanDto plan,
            LearningLanguage language)
        {
            return plan.FormCodes.Contains(unit.FormCode, StringComparer.Ordinal)
                && IsApplicableForm(unit, language)
                && GetApplicableOutputs(unit, plan, language).Count > 0;
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
                value => value == LearningChannel.MeaningWriting
                    ? unit.MeaningWritingCount
                    : unit.DictationCount);
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

        private T SelectWeakest<T>(IReadOnlyList<T> values, Func<T, int> countSelector)
            where T : struct, Enum
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
                    LearningOutputMode.Kana => unit.Variants.Any(variant =>
                        string.Equals(variant.ScriptCode, "Kana", StringComparison.Ordinal)),
                    LearningOutputMode.Kanji => string.Equals(
                            unit.KanjiUsageCode,
                            "Common",
                            StringComparison.Ordinal)
                        && unit.Variants.Any(variant =>
                            string.Equals(variant.ScriptCode, "Kanji", StringComparison.Ordinal)),
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

        private static int GetChannelCount(LearningFormUnitPo unit, LearningChannel channel)
        {
            return channel switch
            {
                LearningChannel.MeaningWriting => unit.MeaningWritingCount,
                LearningChannel.Dictation => unit.DictationCount,
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };
        }

        private static int GetOutputCount(LearningFormUnitPo unit, LearningOutputMode output)
        {
            return output switch
            {
                LearningOutputMode.Latin => unit.LatinCount,
                LearningOutputMode.Kana => unit.KanaCount,
                LearningOutputMode.Kanji => unit.KanjiCount,
                _ => throw new ArgumentOutOfRangeException(nameof(output))
            };
        }

        private static bool IsSameUnit(LearningFormUnitPo left, LearningFormUnitPo right)
        {
            return left.MemberId == right.MemberId
                && string.Equals(left.FormCode, right.FormCode, StringComparison.Ordinal);
        }

        private static bool IsSameUnit(LearningFormUnitPo unit, LearningQuestionHistoryPo question)
        {
            return unit.MemberId == question.MemberId
                && string.Equals(unit.FormCode, question.FormCode, StringComparison.Ordinal);
        }

        private static T ParseEnum<T>(string value, string fieldName) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException($"数据库中的{fieldName}无效：{value}。");
        }

        private sealed record QuestionCandidate(
            LearningFormUnitPo Unit,
            LearningChannel Channel,
            LearningOutputMode Output,
            int UncoveredDimensions,
            double ErrorRate,
            int LastQuestionOrdinal);
    }
}
