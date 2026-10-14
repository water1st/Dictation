using Dictation.Core.Learning;
using Dictation.DataAccess.Practice;
using Dictation.Core.Configuration;

namespace Dictation.Core.Practice
{
    /// <summary>
    /// 依据练习计划、词形状态与答题历史执行纯出题调度决策。
    /// </summary>
    internal sealed class PracticeQuestionScheduler
    {
        private readonly TimeProvider timeProvider;
        private readonly Random random;

        /// <summary>
        /// 初始化出题调度组件，并复用应用级时钟与随机源。
        /// </summary>
        /// <param name="timeProvider">用于记录新题创建时间的时钟。</param>
        /// <param name="random">用于候选选择与重练间隔的随机源。</param>
        public PracticeQuestionScheduler(TimeProvider timeProvider, Random random)
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
        /// <param name="sessionId">所属学习或复习会话的标识。</param>
        /// <param name="language">该会话固定的语言。</param>
        /// <param name="units">该会话独立存储的词形状态。</param>
        /// <param name="history">该会话独立存储的题目历史。</param>
        /// <param name="plan">该模式生效的出题范围与提示能力。</param>
        /// <param name="questionOrdinal">待创建题目的序号。</param>
        /// <returns>下一题持久化输入；无候选时为 <see langword="null"/>。</returns>
        internal CreatePracticeQuestionPo? CreateNextQuestion(
            string sessionId,
            LearningLanguage language,
            IReadOnlyList<PracticeFormUnitPo> units,
            IReadOnlyList<PracticeQuestionHistoryPo> history,
            IPracticeQuestionPlan plan,
            int questionOrdinal)
        {
            var candidates = units
                .Where(unit => !unit.IsMastered && IsEnabledUnit(unit, plan, language))
                .Select(unit => CreateQuestionCandidate(unit, plan, language, history))
                .Where(candidate => candidate is not null)
                .Cast<QuestionCandidate>()
                .ToArray();
            var eligible = candidates
                .Where(candidate => candidate.Unit.NextEligibleOrdinal <= questionOrdinal)
                .ToArray();
            if (eligible.Length == 0)
            {
                eligible = candidates
                    .Where(candidate => MayReleaseEarly(candidate, candidates, history))
                    .ToArray();
            }

            if (eligible.Length == 0)
            {
                return null;
            }

            // 优先补齐尚未覆盖的通道和字形；覆盖程度相同时，再优先安排历史错误率更高的词形。
            var maxUncovered = eligible.Max(candidate => candidate.UncoveredDimensions);
            var coveragePool = eligible
                .Where(candidate => candidate.UncoveredDimensions == maxUncovered)
                .ToArray();
            var maxErrorRate = coveragePool.Max(candidate => candidate.ErrorRate);
            var priorityPool = coveragePool
                .Where(candidate => Math.Abs(candidate.ErrorRate - maxErrorRate) < double.Epsilon)
                .ToArray();
            var selected = priorityPool[random.Next(priorityPool.Length)];
            return new CreatePracticeQuestionPo(
                sessionId,
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
        /// <param name="plan">当前语言生效的练习选项。</param>
        /// <param name="language">当前会话语言。</param>
        /// <returns>词形在当前计划中可练习时返回 <see langword="true"/>。</returns>
        internal bool IsEnabledUnit(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language)
        {
            return plan.FormCodes.Contains(unit.FormCode, StringComparer.Ordinal)
                && IsApplicableForm(unit, language)
                && GetApplicableOutputs(unit, plan, language).Count > 0;
        }

        /// <summary>按目标自身的计数检查本次观察后覆盖，不能以共享主题的调度代替次要目标缺口。</summary>
        /// <param name="unit">该目标尚未计入本次观察的词形状态。</param>
        /// <param name="question">本次真实题型及字形。</param>
        /// <param name="plan">目标所需的启用范围。</param>
        /// <param name="language">用于排除不适用字形的语言。</param>
        /// <returns>全部启用渠道及适用字形已出现时返回真。</returns>
        internal bool HasRequiredCoverage(PracticeFormUnitPo unit, PracticeQuestionStatePo question,
            IPracticeQuestionPlan plan, LearningLanguage language)
        {
            return plan.Channels.All(channel => GetChannelCount(unit, channel)
                    + (question.Channel == channel.ToString() ? 1 : 0) > 0)
                && GetApplicableOutputs(unit, plan, language).All(output => GetOutputCount(unit, output)
                    + (question.OutputMode == output.ToString() ? 1 : 0) > 0);
        }

        /// <summary>
        /// 判断当前计划中是否仍有答错且尚未按同一题型和字形答对的题目。
        /// </summary>
        /// <param name="unit">待检查的成员词形。</param>
        /// <param name="plan">当前模式生效的练习选项。</param>
        /// <param name="language">会话语言。</param>
        /// <param name="history">包含本次答题结果的会话历史。</param>
        /// <returns>仍需重练时为 <see langword="true"/>。</returns>
        internal bool HasUnresolvedRetry(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language,
            IReadOnlyList<PracticeQuestionHistoryPo> history)
        {
            return FindUnresolvedRetry(unit, plan, language, history) is not null;
        }

        /// <summary>
        /// 为词形选择练习次数最少的通道和可用字形，并汇总未覆盖维度与历史错误率，
        /// 供外层先按覆盖缺口、再按错误率确定调度优先级。
        /// </summary>
        /// <param name="unit">待评估的词形状态。</param>
        /// <param name="plan">当前语言生效的练习选项。</param>
        /// <param name="language">当前会话语言。</param>
        /// <param name="history">本轮已创建题目的历史。</param>
        /// <returns>可参与优先级比较的候选；没有适用字形时返回 <see langword="null"/>。</returns>
        private QuestionCandidate? CreateQuestionCandidate(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language,
            IReadOnlyList<PracticeQuestionHistoryPo> history)
        {
            var outputs = GetApplicableOutputs(unit, plan, language);
            if (outputs.Count == 0)
            {
                return null;
            }

            var hasUncoveredChannel = plan.Channels.Any(value => GetChannelCount(unit, value) == 0);
            var hasUncoveredOutput = outputs.Any(value => GetOutputCount(unit, value) == 0);
            // 先覆盖计划中的题型与字形，再回到曾经答错的精确题型／字形组合。
            var retry = hasUncoveredChannel || hasUncoveredOutput
                ? null
                : FindUnresolvedRetry(unit, plan, language, history);
            var channel = retry is null
                ? SelectWeakest(plan.Channels, value => GetChannelCount(unit, value))
                : ParseEnum<LearningChannel>(retry.Channel, "题型");
            var output = retry is null
                ? SelectWeakest(outputs, value => GetOutputCount(unit, value))
                : ParseEnum<LearningOutputMode>(retry.OutputMode, "输出方式");
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

        private static PracticeQuestionHistoryPo? FindUnresolvedRetry(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language,
            IReadOnlyList<PracticeQuestionHistoryPo> history)
        {
            var outputs = GetApplicableOutputs(unit, plan, language)
                .Select(value => value.ToString())
                .ToHashSet(StringComparer.Ordinal);
            var channels = plan.Channels.Select(value => value.ToString())
                .ToHashSet(StringComparer.Ordinal);
            var examined = new HashSet<(string Channel, string OutputMode)>();
            foreach (var question in history
                .Where(item => IsSameUnit(unit, item) && item.WasCorrect.HasValue)
                .OrderByDescending(item => item.QuestionOrdinal))
            {
                if (!channels.Contains(question.Channel)
                    || !outputs.Contains(question.OutputMode)
                    || !examined.Add((question.Channel, question.OutputMode)))
                {
                    continue;
                }

                if (question.WasCorrect == false)
                {
                    return question;
                }
            }

            return null;
        }

        /// <summary>
        /// 在计数最少的选项中随机选择，平衡题型或输出方式的覆盖。
        /// </summary>
        private T SelectWeakest<T>(IReadOnlyList<T> values, Func<T, int> countSelector)
            where T : struct, Enum
        {
            var minimum = values.Min(countSelector);
            var pool = values.Where(value => countSelector(value) == minimum).ToArray();
            return pool[random.Next(pool.Length)];
        }

        /// <summary>
        /// 判断常规间隔尚未到达的词形能否在小候选池中提前释放。
        /// 只有其余每个词形都在该词形上次出题之后至少完成过一题，才视为已有足够穿插；
        /// 没有其他词形时该条件自然成立，避免单词形会话无法继续。
        /// </summary>
        /// <param name="candidate">等待提前释放的候选。</param>
        /// <param name="candidates">当前全部未掌握且可练习的候选。</param>
        /// <param name="history">用于判断各词形最近穿插顺序的答题历史。</param>
        /// <returns>已满足小池穿插边界时返回 <see langword="true"/>。</returns>
        private static bool MayReleaseEarly(
            QuestionCandidate candidate,
            IReadOnlyList<QuestionCandidate> candidates,
            IReadOnlyList<PracticeQuestionHistoryPo> history)
        {
            return candidates
                .Where(other => !IsSameUnit(candidate.Unit, other.Unit)
                    && !WereJointlyAnswered(candidate.Unit, other.Unit, candidate.LastQuestionOrdinal, history))
                .All(other => history.Any(item =>
                    item.WasCorrect.HasValue
                    && item.QuestionOrdinal > candidate.LastQuestionOrdinal
                    && IsSameUnit(other.Unit, item)));
        }

        /// <summary>共享同一次听写观察的目标不互相充当穿插障碍，避免小池无法推进。</summary>
        /// <param name="unit">需要重练的词形。</param>
        /// <param name="other">待检查的另一词形。</param>
        /// <param name="lastOrdinal">本词形最近出题序号。</param>
        /// <param name="history">本轮真实作答历史。</param>
        private static bool WereJointlyAnswered(PracticeFormUnitPo unit, PracticeFormUnitPo other, int lastOrdinal,
            IReadOnlyList<PracticeQuestionHistoryPo> history)
        {
            var last = history.FirstOrDefault(item => item.WasCorrect.HasValue && item.QuestionOrdinal == lastOrdinal && IsSameUnit(unit, item));
            return last is not null && history.Any(item => item.WasCorrect.HasValue
                && item.QuestionId == last.QuestionId && item.QuestionOrdinal == lastOrdinal && IsSameUnit(other, item));
        }

        /// <summary>
        /// 将计划字形限制到词形实际具备的表示：英语只使用存在变体的拉丁字形；
        /// 日语假名必须有假名变体，汉字还必须标记为常用且存在汉字变体，从而排除罕见或无汉字词形。
        /// </summary>
        /// <param name="unit">待筛选字形的词形状态。</param>
        /// <param name="plan">当前模式声明的启用字形。</param>
        /// <param name="language">当前会话语言。</param>
        /// <returns>同时满足计划选择和词形实际表示能力的字形。</returns>
        private static IReadOnlyList<LearningOutputMode> GetApplicableOutputs(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language)
        {
            return plan.OutputModes.Where(output => PracticeFormApplicability.HasOutput(
                language, unit.KanjiUsageCode, unit.Variants.Select(variant => variant.ScriptCode), output)).ToArray();
        }

        private static bool IsApplicableForm(PracticeFormUnitPo unit, LearningLanguage language)
        {
            return PracticeFormApplicability.IsFormApplicable(language, unit.IsPhrase, unit.FormCode);
        }

        private static int GetChannelCount(PracticeFormUnitPo unit, LearningChannel channel)
        {
            return channel switch
            {
                LearningChannel.MeaningWriting => unit.MeaningWritingCount,
                LearningChannel.Dictation => unit.DictationCount,
                _ => throw new ArgumentOutOfRangeException(nameof(channel))
            };
        }

        private static int GetOutputCount(PracticeFormUnitPo unit, LearningOutputMode output)
        {
            return output switch
            {
                LearningOutputMode.Latin => unit.LatinCount,
                LearningOutputMode.Kana => unit.KanaCount,
                LearningOutputMode.Kanji => unit.KanjiCount,
                _ => throw new ArgumentOutOfRangeException(nameof(output))
            };
        }

        private static bool IsSameUnit(PracticeFormUnitPo left, PracticeFormUnitPo right)
        {
            return left.MemberId == right.MemberId
                && string.Equals(left.FormCode, right.FormCode, StringComparison.Ordinal);
        }

        private static bool IsSameUnit(PracticeFormUnitPo unit, PracticeQuestionHistoryPo question)
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
            PracticeFormUnitPo Unit,
            LearningChannel Channel,
            LearningOutputMode Output,
            int UncoveredDimensions,
            double ErrorRate,
            int LastQuestionOrdinal);
    }
}
