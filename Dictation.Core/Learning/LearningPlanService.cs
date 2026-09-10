using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 负责校验和持久化学习计划、读取学习库，并依据词库完整词组生成英语示例范式。
    /// </summary>
    public sealed class LearningPlanService
    {
        private static readonly HashSet<string> JapaneseFormCodes = new(StringComparer.Ordinal)
        {
            "Dictionary", "Polite", "Te", "Ta"
        };

        private static readonly HashSet<string> EnglishFormCodes = new(StringComparer.Ordinal)
        {
            "Base", "PresentFirstPersonSingular", "PresentNonThirdPerson", "ThirdPersonSingular",
            "Past", "PastParticiple", "PresentParticiple", "Plural", "Comparative", "Superlative"
        };

        private readonly LearningPlanDataAccessor dataAccessor;
        private readonly ILexiconImportGroupResolver lexiconImportGroupResolver;
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// 初始化学习计划服务。
        /// </summary>
        /// <param name="dataAccessor">用于读写学习计划和学习库的数据访问器。</param>
        /// <param name="lexiconImportGroupResolver">用于校验英语示例词及解析完整词组的词库能力。</param>
        /// <param name="timeProvider">用于记录计划更新时间和生成默认计划时间的时钟。</param>
        public LearningPlanService(
            LearningPlanDataAccessor dataAccessor,
            ILexiconImportGroupResolver lexiconImportGroupResolver,
            TimeProvider timeProvider)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.lexiconImportGroupResolver = lexiconImportGroupResolver ?? throw new ArgumentNullException(nameof(lexiconImportGroupResolver));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// 读取指定语言的学习计划；尚未保存计划时生成默认计划。
        /// </summary>
        /// <param name="language">学习计划所属语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>已保存的计划，或尚无记录时生成的默认计划。</returns>
        public async Task<LearningPlanDto> LoadAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await dataAccessor.GetPlanAsync(language.ToString(), cancellationToken);
            return plan is null
                ? CreateDefault(language)
                : await MapPlanAsync(plan, cancellationToken);
        }

        /// <summary>
        /// 校验并保存学习计划；示例范式模式下同时从词库解析所选动词的启用词形。
        /// </summary>
        /// <param name="request">待保存的计划选项。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>规范化并补全示例范式后的学习计划。</returns>
        public async Task<LearningPlanDto> SaveAsync(
            SaveLearningPlanRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = ValidateAndNormalize(request);
            var example = normalized.Language == LearningLanguage.English
                && normalized.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                    ? await ResolveExampleParadigmAsync(
                        normalized.EnglishExampleSourceSenseId,
                        normalized.FormCodes,
                        cancellationToken)
                    : null;
            var plan = new LearningPlanPo(
                normalized.Language.ToString(),
                normalized.DailyNewTarget,
                normalized.SessionWordLimit,
                normalized.SelectionMode.ToString(),
                normalized.EnglishFormLabelMode.ToString(),
                normalized.EnglishExampleSourceSenseId,
                normalized.FormCodes,
                normalized.Channels.Select(channel => channel.ToString()).ToArray(),
                normalized.OutputModes.Select(output => output.ToString()).ToArray(),
                timeProvider.GetUtcNow());
            await dataAccessor.SavePlanAsync(plan, cancellationToken);
            return MapPlan(plan, example);
        }

        /// <summary>
        /// 分页读取指定语言的学习库整组词条。
        /// </summary>
        /// <param name="language">学习库所属语言。</param>
        /// <param name="pageNumber">从 1 开始的页码。</param>
        /// <param name="pageSize">每页数量，最大为 500。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含总数和当前页整组词条的分页结果。</returns>
        public async Task<LearningLibraryPageDto> GetLibraryPageAsync(
            LearningLanguage language,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 500);
            cancellationToken.ThrowIfCancellationRequested();
            var page = await dataAccessor.GetLearningLibraryPageAsync(
                language.ToString(),
                checked((pageNumber - 1) * pageSize),
                pageSize,
                cancellationToken);
            return new LearningLibraryPageDto(
                pageNumber,
                pageSize,
                page.TotalCount,
                page.Groups.Select(MapGroup).ToArray());
        }

        /// <summary>
        /// 删除指定语言学习库中的完整词组及其学习进度。
        /// </summary>
        /// <param name="language">词组所属学习语言。</param>
        /// <param name="wordGroupId">要删除的词组稳定标识。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        public async Task DeleteWordGroupAsync(
            LearningLanguage language,
            string wordGroupId,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentException.ThrowIfNullOrWhiteSpace(wordGroupId);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await dataAccessor.DeleteWordGroupAsync(language.ToString(), wordGroupId.Trim(), cancellationToken);
            switch (result)
            {
                case DeleteLearningWordGroupResult.Deleted:
                    return;
                case DeleteLearningWordGroupResult.NotFound:
                    throw new KeyNotFoundException("学习库词组不存在。");
                case DeleteLearningWordGroupResult.WrongLanguage:
                case DeleteLearningWordGroupResult.WrongPlacement:
                    throw new InvalidOperationException("只能删除当前语言学习库中的整组词条。");
                default:
                    throw new InvalidOperationException("未知的学习库删除结果。");
            }
        }

        /// <summary>
        /// 根据已保存计划生成一轮学习的启动请求，并在手动模式下校验所选词组均属于当前学习库。
        /// </summary>
        /// <param name="language">本轮学习语言。</param>
        /// <param name="selectedWordGroupIds">手动模式下选择的词组标识；其他模式下不参与范围选择。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含选词模式、数量上限及已验证手动词组的启动请求。</returns>
        public async Task<LearningStartRequestDto> CreateStartRequestAsync(
            LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await LoadAsync(language, cancellationToken);
            if (plan.SelectionMode != LearningSelectionMode.Manual)
            {
                return new LearningStartRequestDto(language, plan.SessionWordLimit, plan.SelectionMode, Array.Empty<string>());
            }

            if (selectedWordGroupIds is null || selectedWordGroupIds.Count == 0)
            {
                throw new ArgumentException("手动选词时至少需要选择一个词组。", nameof(selectedWordGroupIds));
            }

            var normalizedIds = selectedWordGroupIds.Select(id => NormalizeText(id, "词组标识")).ToArray();
            if (normalizedIds.Distinct(StringComparer.Ordinal).Count() != normalizedIds.Length)
            {
                throw new ArgumentException("手动选择的词组不能重复。", nameof(selectedWordGroupIds));
            }

            var existingCount = await dataAccessor.CountLearningWordGroupsAsync(language.ToString(), normalizedIds, cancellationToken);
            if (existingCount != normalizedIds.Length)
            {
                throw new ArgumentException("选择中包含不存在、其他语言或非学习库词组。", nameof(selectedWordGroupIds));
            }

            return new LearningStartRequestDto(language, plan.SessionWordLimit, plan.SelectionMode, normalizedIds);
        }

        /// <summary>
        /// 校验计划各选项的语言约束，并规范化词形、渠道、输出形式和示例义项。
        /// </summary>
        /// <param name="request">原始保存请求。</param>
        /// <returns>去重并规范化后的保存请求。</returns>
        private SaveLearningPlanRequestDto ValidateAndNormalize(SaveLearningPlanRequestDto request)
        {
            ValidateLanguage(request.Language);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.DailyNewTarget, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.DailyNewTarget, 500);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.SessionWordLimit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.SessionWordLimit, 500);
            ValidateEnum(request.SelectionMode, nameof(request.SelectionMode));
            ValidateEnum(request.EnglishFormLabelMode, nameof(request.EnglishFormLabelMode));
            var forms = NormalizeForms(request.FormCodes, request.Language);
            var channels = NormalizeEnums(request.Channels, nameof(request.Channels));
            var outputs = NormalizeEnums(request.OutputModes, nameof(request.OutputModes));
            ValidateLanguageOptions(request, outputs);
            var exampleSourceSenseId = request.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                ? NormalizeText(request.EnglishExampleSourceSenseId, "英语示例词义项")
                : null;
            return request with
            {
                EnglishExampleSourceSenseId = exampleSourceSenseId,
                FormCodes = forms,
                Channels = channels,
                OutputModes = outputs
            };
        }

        private static IReadOnlyList<string> NormalizeForms(IReadOnlyList<string> forms, LearningLanguage language)
        {
            if (forms is null || forms.Count == 0)
            {
                throw new ArgumentException("至少需要启用一个词形。", nameof(forms));
            }

            var allowed = language == LearningLanguage.Japanese ? JapaneseFormCodes : EnglishFormCodes;
            var normalized = forms.Select(form => NormalizeText(form, "词形标识")).Distinct(StringComparer.Ordinal).ToArray();
            if (normalized.Any(form => !allowed.Contains(form)))
            {
                throw new ArgumentException("所选词形不适用于当前语言。", nameof(forms));
            }

            return normalized;
        }

        private static IReadOnlyList<T> NormalizeEnums<T>(IReadOnlyList<T> values, string parameterName)
            where T : struct, Enum
        {
            if (values is null || values.Count == 0)
            {
                throw new ArgumentException("至少需要启用一项。", parameterName);
            }

            if (values.Any(value => !Enum.IsDefined(value)))
            {
                throw new ArgumentOutOfRangeException(parameterName, "包含不受支持的选项。");
            }

            return values.Distinct().ToArray();
        }

        private static void ValidateLanguageOptions(
            SaveLearningPlanRequestDto request,
            IReadOnlyList<LearningOutputMode> outputs)
        {
            if (request.Language == LearningLanguage.Japanese)
            {
                if (request.EnglishFormLabelMode != EnglishFormLabelMode.GrammarName
                    || !string.IsNullOrWhiteSpace(request.EnglishExampleSourceSenseId))
                {
                    throw new ArgumentException("日语计划不能设置英语词形标签选项。", nameof(request));
                }

                if (outputs.Any(output => output == LearningOutputMode.Latin)
                    || !outputs.Any(output => output is LearningOutputMode.Kana or LearningOutputMode.Kanji))
                {
                    throw new ArgumentException("日语至少需要启用假名或汉字，且不能使用拉丁字母输出。", nameof(request.OutputModes));
                }
            }
            else if (outputs.Count != 1 || outputs[0] != LearningOutputMode.Latin)
            {
                throw new ArgumentException("英语只支持拉丁字母输出。", nameof(request.OutputModes));
            }
        }

        /// <summary>
        /// 将持久化计划映射为业务对象，并按需解析英语示例范式。
        /// </summary>
        /// <param name="plan">持久化的学习计划。</param>
        /// <param name="cancellationToken">取消词库解析的令牌。</param>
        /// <returns>包含可展示示例范式的学习计划。</returns>
        private async Task<LearningPlanDto> MapPlanAsync(LearningPlanPo plan, CancellationToken cancellationToken)
        {
            var language = ParseEnum<LearningLanguage>(plan.Language, "学习语言");
            var formCodes = plan.FormCodes.ToArray();
            var labelMode = ParseEnum<EnglishFormLabelMode>(plan.EnglishFormLabelMode, "英语词形标签模式");
            var example = language == LearningLanguage.English && labelMode == EnglishFormLabelMode.ExampleParadigm
                ? await ResolveExampleParadigmAsync(plan.EnglishExampleSourceSenseId, formCodes, cancellationToken)
                : null;
            return MapPlan(plan, example);
        }

        /// <summary>
        /// 将持久化字段转换为强类型计划，并附加已解析的示例范式。
        /// </summary>
        /// <param name="plan">持久化的学习计划。</param>
        /// <param name="example">示例范式；未启用示例模式时为 <see langword="null"/>。</param>
        /// <returns>可供表现层使用的学习计划。</returns>
        private static LearningPlanDto MapPlan(
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
        /// 从英语完整词组中定位指定单词动词，并为每个启用词形选择优先、常用的展示形式。
        /// </summary>
        /// <param name="sourceSenseId">示例词的来源义项稳定标识。</param>
        /// <param name="enabledForms">计划中启用的英语词形代码。</param>
        /// <param name="cancellationToken">取消词库解析的令牌。</param>
        /// <returns>与已启用词形一一对应的英语示例范式。</returns>
        private async Task<EnglishExampleParadigmDto> ResolveExampleParadigmAsync(
            string? sourceSenseId,
            IReadOnlyList<string> enabledForms,
            CancellationToken cancellationToken)
        {
            var normalizedId = NormalizeText(sourceSenseId, "英语示例词义项");
            var group = await lexiconImportGroupResolver.GetImportGroupAsync(LearningLanguage.English, normalizedId, cancellationToken);
            var member = group.SingleOrDefault(item => string.Equals(item.SourceSenseId, normalizedId, StringComparison.Ordinal));
            if (member is null || member.PartOfSpeechLabel != "动词" || member.IsPhrase)
            {
                throw new ArgumentException("英语示例词必须是词库中存在的单词动词。", nameof(sourceSenseId));
            }

            var forms = new List<EnglishExampleFormDto>(enabledForms.Count);
            foreach (var formCode in enabledForms)
            {
                var form = member.Forms
                    .Where(item => string.Equals(item.FormCode, formCode, StringComparison.Ordinal))
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.IsCommon)
                    .FirstOrDefault();
                if (form is null)
                {
                    throw new ArgumentException("英语示例词缺少已启用的词形。", nameof(sourceSenseId));
                }

                forms.Add(new EnglishExampleFormDto(formCode, form.Surface));
            }

            return new EnglishExampleParadigmDto(normalizedId, member.Headword, forms);
        }

        /// <summary>
        /// 为尚无持久化记录的语言创建默认学习计划。
        /// </summary>
        /// <param name="language">默认计划所属语言。</param>
        /// <returns>使用该语言默认词形与输出方式的未持久化计划。</returns>
        private LearningPlanDto CreateDefault(LearningLanguage language)
        {
            return new LearningPlanDto(
                language,
                20,
                20,
                LearningSelectionMode.Random,
                EnglishFormLabelMode.GrammarName,
                null,
                language == LearningLanguage.Japanese ? new[] { "Dictionary" } : new[] { "Base" },
                new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                language == LearningLanguage.Japanese
                    ? new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }
                    : new[] { LearningOutputMode.Latin },
                timeProvider.GetUtcNow(),
                null);
        }

        /// <summary>
        /// 将学习库中的持久化词组映射为包含全部成员的业务对象。
        /// </summary>
        /// <param name="group">持久化的学习词组。</param>
        /// <returns>供学习计划页面展示的整组词条。</returns>
        private static LearningWordGroupDto MapGroup(LearningWordGroupPo group)
        {
            return new LearningWordGroupDto(
                group.WordGroupId,
                ParseEnum<LearningLanguage>(group.Language, "学习语言"),
                group.AnchorSourceSenseId,
                group.CreatedAtUtc,
                group.Members.Select(MapMember).ToArray());
        }

        /// <summary>
        /// 将学习库成员还原为导入词组成员；非简体中文释义按无可用中文释义处理。
        /// </summary>
        /// <param name="member">持久化的词组成员。</param>
        /// <returns>保留词形与释义元数据的业务成员。</returns>
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
                string.Equals(member.MeaningLanguage, "zh-CN", StringComparison.Ordinal) ? member.Meanings : Array.Empty<string>(),
                member.Forms.Select(form => new ImportWordGroupFormDto(
                    form.Surface, form.Reading, form.FormCode, form.ScriptCode, form.RoleCode,
                    form.FormLabel, form.DetailLabel, form.IsCommon, form.IsPrimary)).ToArray(),
                ParseEnumOrDefault(member.MeaningSource, LexiconMeaningSource.Missing),
                ParseEnumOrDefault(member.MeaningPrecision, LexiconMeaningPrecision.Missing),
                member.KanjiUsageCode);
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            ValidateEnum(language, nameof(language));
        }

        private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "选项不受支持。");
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

        private static string NormalizeText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName}不能为空。", fieldName);
            }

            return value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
