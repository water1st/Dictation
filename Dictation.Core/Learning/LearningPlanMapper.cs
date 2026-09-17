using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.DataAccess.Learning;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 在学习计划、学习库持久化对象与业务 DTO 之间执行纯转换。
    /// </summary>
    internal static class LearningPlanMapper
    {
        /// <summary>
        /// 将持久化计划转换为强类型计划，并附加已由业务层解析的示例范式。
        /// </summary>
        internal static LearningPlanDto ToDto(
            LearningPlanPo plan,
            EnglishExampleParadigmDto? example)
        {
            return new LearningPlanDto(
                ParseEnum<LearningLanguage>(plan.Language, "学习语言"),
                plan.DailyNewTarget,
                plan.SessionWordLimit,
                ParseEnum<LearningSelectionMode>(plan.SelectionMode, "选词模式"),
                ParseEnum<EnglishFormLabelMode>(plan.EnglishFormLabelMode, "英语词形标签模式"),
                plan.EnglishExampleSourceSenseId,
                plan.FormCodes.ToArray(),
                plan.Channels.Select(channel => ParseEnum<LearningChannel>(channel, "出题渠道")).ToArray(),
                plan.OutputModes.Select(output => ParseEnum<LearningOutputMode>(output, "输出形式")).ToArray(),
                plan.UpdatedAtUtc,
                example);
        }

        /// <summary>
        /// 将业务层已校验并规范化的保存请求转换为持久化计划。
        /// </summary>
        internal static LearningPlanPo ToPo(
            SaveLearningPlanRequestDto request,
            DateTimeOffset updatedAtUtc)
        {
            return new LearningPlanPo(
                request.Language.ToString(),
                request.DailyNewTarget,
                request.SessionWordLimit,
                request.SelectionMode.ToString(),
                request.EnglishFormLabelMode.ToString(),
                request.EnglishExampleSourceSenseId,
                request.FormCodes.ToArray(),
                request.Channels.Select(channel => channel.ToString()).ToArray(),
                request.OutputModes.Select(output => output.ToString()).ToArray(),
                updatedAtUtc);
        }

        /// <summary>
        /// 将持久化学习库分页结果转换为包含完整词组的业务分页结果。
        /// </summary>
        internal static LearningLibraryPageDto ToDto(
            LearningLibraryPagePo page,
            int pageNumber,
            int pageSize)
        {
            return new LearningLibraryPageDto(
                pageNumber,
                pageSize,
                page.TotalCount,
                page.Groups.Select(ToDto).ToArray());
        }

        /// <summary>
        /// 投影学习库中的单个词组快照，复用共享成员映射。
        /// </summary>
        private static LearningWordGroupDto ToDto(LearningWordGroupPo group)
        {
            return new LearningWordGroupDto(
                group.WordGroupId,
                ParseEnum<LearningLanguage>(group.Language, "学习语言"),
                group.AnchorSourceSenseId,
                group.CreatedAtUtc,
                group.Members.Select(ImportWordGroupMapper.ToDto).ToArray());
        }

        private static T ParseEnum<T>(string value, string fieldName) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException($"数据库中的{fieldName}无效：{value}。");
        }
    }
}
