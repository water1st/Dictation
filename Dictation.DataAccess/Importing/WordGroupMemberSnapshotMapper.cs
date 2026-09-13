namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 将词组成员、释义和词形行转换为按词组索引的持久化快照。
    /// </summary>
    internal static class WordGroupMemberSnapshotMapper
    {
        /// <summary>
        /// 按持久化顺序装配完整的词组成员快照。
        /// </summary>
        /// <param name="members">待装配的词组成员行。</param>
        /// <param name="meanings">待装配的释义行。</param>
        /// <param name="forms">待装配的词形行。</param>
        /// <returns>以词组标识为键、成员快照为值的只读映射。</returns>
        internal static IReadOnlyDictionary<string, IReadOnlyList<ImportWordGroupMemberPo>> Map(
            IReadOnlyList<WordGroupMemberRow> members,
            IReadOnlyList<WordGroupMeaningRow> meanings,
            IReadOnlyList<WordGroupFormRow> forms)
        {
            ArgumentNullException.ThrowIfNull(members);
            ArgumentNullException.ThrowIfNull(meanings);
            ArgumentNullException.ThrowIfNull(forms);

            var meaningsByMember = meanings
                .GroupBy(meaning => meaning.MemberId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .OrderBy(meaning => meaning.MeaningIndex)
                        .Select(meaning => meaning.Text)
                        .ToArray());
            var formsByMember = forms
                .GroupBy(form => form.MemberId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ImportWordGroupFormPo>)group
                        .OrderBy(form => form.FormIndex)
                        .Select(MapForm)
                        .ToArray());

            return members
                .GroupBy(member => member.WordGroupId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ImportWordGroupMemberPo>)group
                        .OrderBy(member => member.SortOrder)
                        .Select(member => MapMember(member, meaningsByMember, formsByMember))
                        .ToArray(),
                    StringComparer.Ordinal);
        }

        private static ImportWordGroupMemberPo MapMember(
            WordGroupMemberRow member,
            IReadOnlyDictionary<long, IReadOnlyList<string>> meaningsByMember,
            IReadOnlyDictionary<long, IReadOnlyList<ImportWordGroupFormPo>> formsByMember)
        {
            return new ImportWordGroupMemberPo(
                member.Language,
                member.SourceSenseId,
                member.SourceLexemeId,
                member.Headword,
                member.Reading,
                member.PartOfSpeechLabel,
                member.AttributeLabel,
                member.IsPhrase != 0,
                member.MeaningLanguage,
                member.MeaningSource,
                member.MeaningPrecision,
                meaningsByMember.TryGetValue(member.MemberId, out var meanings)
                    ? meanings
                    : Array.Empty<string>(),
                formsByMember.TryGetValue(member.MemberId, out var forms)
                    ? forms
                    : Array.Empty<ImportWordGroupFormPo>(),
                member.KanjiUsageCode);
        }

        private static ImportWordGroupFormPo MapForm(WordGroupFormRow form)
        {
            return new ImportWordGroupFormPo(
                form.Surface,
                form.Reading,
                form.FormCode,
                form.ScriptCode,
                form.RoleCode,
                form.FormLabel,
                form.DetailLabel,
                form.IsCommon != 0,
                form.IsPrimary != 0);
        }
    }
}
