using Dictation.Core.Lexicon;
using Dictation.DataAccess.Importing;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 在词库结果、持久化快照和可消费的词组业务对象之间执行纯转换。
    /// </summary>
    internal static class ImportWordGroupMapper
    {
        /// <summary>
        /// 将一个已确定的词库成员固化为导入快照成员。
        /// </summary>
        /// <param name="value">词库返回的完整成员。</param>
        /// <param name="meaningOverride">已由业务层确定的中文释义覆盖；为 <see langword="null"/> 时保留词库释义元数据。</param>
        /// <returns>可随词组持久化的成员快照。</returns>
        internal static ImportWordGroupMemberPo ToSnapshotPo(
            LexiconLookupResultDto value,
            IReadOnlyList<string>? meaningOverride)
        {
            var isUserOverride = meaningOverride is not null;
            return new ImportWordGroupMemberPo(
                value.Language.ToString(),
                value.SourceSenseId,
                value.SourceLexemeId,
                value.Headword,
                value.Reading,
                value.PartOfSpeechLabel,
                value.AttributeLabel,
                value.IsPhrase,
                "zh-CN",
                isUserOverride ? LexiconMeaningSource.User.ToString() : value.MeaningSource.ToString(),
                isUserOverride ? LexiconMeaningPrecision.UserProvided.ToString() : value.MeaningPrecision.ToString(),
                meaningOverride ?? value.Meanings,
                value.Forms.Select(ToSnapshotPo).ToArray(),
                value.KanjiUsageCode);
        }

        /// <summary>
        /// 将持久化词组还原为包含全部成员的业务快照。
        /// </summary>
        /// <param name="group">持久化词组。</param>
        /// <returns>可供导入与学习消费者读取的完整词组。</returns>
        internal static ImportWordGroupDto ToDto(ImportWordGroupPo group)
        {
            return new ImportWordGroupDto(
                group.WordGroupId,
                Enum.Parse<Configuration.LearningLanguage>(group.Language, ignoreCase: false),
                group.AnchorSourceSenseId,
                Enum.Parse<ImportWordGroupPlacement>(group.Placement, ignoreCase: false),
                group.CreatedAtUtc,
                group.Members.Select(ToDto).ToArray());
        }

        /// <summary>
        /// 将持久化词组成员还原为业务快照；非简体中文释义不会作为中文输出暴露。
        /// </summary>
        /// <param name="member">持久化成员。</param>
        /// <returns>保留词形与来源元数据的成员业务对象。</returns>
        internal static ImportWordGroupMemberDto ToDto(ImportWordGroupMemberPo member)
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
                member.Forms.Select(ToDto).ToArray(),
                ParseOrDefault(member.MeaningSource, LexiconMeaningSource.Missing),
                ParseOrDefault(member.MeaningPrecision, LexiconMeaningPrecision.Missing),
                member.KanjiUsageCode);
        }

        /// <summary>
        /// 将词库词形投影为导入词形快照，保留字形、角色和优先级元数据。
        /// </summary>
        private static ImportWordGroupFormPo ToSnapshotPo(LexiconLookupFormDto form)
        {
            return new ImportWordGroupFormPo(
                form.Surface,
                form.Reading,
                form.FormCode,
                form.ScriptCode,
                form.RoleCode,
                form.FormLabel,
                form.DetailLabel,
                form.IsCommon,
                form.IsPrimary);
        }

        /// <summary>
        /// 将持久化词形快照投影为业务词形，保留标签及常用、主形式标记。
        /// </summary>
        private static ImportWordGroupFormDto ToDto(ImportWordGroupFormPo form)
        {
            return new ImportWordGroupFormDto(
                form.Surface,
                form.Reading,
                form.FormCode,
                form.ScriptCode,
                form.RoleCode,
                form.FormLabel,
                form.DetailLabel,
                form.IsCommon,
                form.IsPrimary);
        }

        private static T ParseOrDefault<T>(string value, T defaultValue) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : defaultValue;
        }
    }
}
