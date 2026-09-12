using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.DataAccess.Learning;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 将学习会话及浏览查询的持久化字段纯投影为业务 DTO。
    /// </summary>
    internal static class LearningSessionMapper
    {
        /// <summary>
        /// 将持久化会话概要转换为强类型业务会话。
        /// </summary>
        internal static LearningSessionDto ToDto(LearningSessionPo session)
        {
            return new LearningSessionDto(
                session.SessionId,
                ParseEnum<LearningLanguage>(session.Language, "学习语言"),
                ParseEnum<LearningSessionStatus>(session.Status, "学习会话状态"),
                session.CurrentOrdinal,
                session.CreatedAtUtc,
                session.CompletedAtUtc,
                session.WordGroupIds.ToArray());
        }

        /// <summary>
        /// 将会话浏览查询结果转换为完整词组业务对象，并复用统一词组成员映射。
        /// </summary>
        internal static LearningBrowseItemDto ToDto(LearningBrowseItemPo item)
        {
            return new LearningBrowseItemDto(
                item.SortOrder,
                item.WordGroupId,
                item.Members.Select(ImportWordGroupMapper.ToDto).ToArray());
        }

        private static T ParseEnum<T>(string value, string fieldName) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidDataException($"数据库中的{fieldName}无效：{value}。");
        }
    }
}
