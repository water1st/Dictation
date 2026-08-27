using System.Globalization;
using Dapper;
using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Importing
{
    public sealed class ImportInboxDataAccessor
    {
        private static readonly HashSet<string> Languages = new(StringComparer.Ordinal)
        {
            "Japanese",
            "English"
        };

        private static readonly HashSet<string> SourceKinds = new(StringComparer.Ordinal)
        {
            "Manual",
            "Text",
            "Mcp"
        };

        private static readonly HashSet<string> Decisions = new(StringComparer.Ordinal)
        {
            "Learning",
            "Review",
            "Discard"
        };

        private readonly SqliteConnectionFactory connectionFactory;

        public ImportInboxDataAccessor(SqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<ImportBatchPo> StageAsync(
            ImportBatchStagePo request,
            CancellationToken cancellationToken = default)
        {
            ValidateStageRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                var batchId = Guid.NewGuid().ToString("N");
                var createdAtUtc = DateTimeOffset.UtcNow;

                try
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO ImportBatches (BatchId, Language, SourceKind, Status, CreatedAtUtc, CompletedAtUtc)
                        VALUES (@BatchId, @Language, @SourceKind, 'Pending', @CreatedAtUtc, NULL);
                        """,
                        new
                        {
                            BatchId = batchId,
                            request.Language,
                            request.SourceKind,
                            CreatedAtUtc = FormatDateTime(createdAtUtc)
                        },
                        transaction,
                        cancellationToken: cancellationToken));

                    foreach (var item in request.Items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var memberSenseIds = item.Group.Members
                            .Select(member => member.SourceSenseId)
                            .ToArray();
                        var duplicate = await HasExistingMemberAsync(
                            connection,
                            transaction,
                            request.Language,
                            memberSenseIds,
                            cancellationToken);
                        string? wordGroupId = null;
                        if (!duplicate)
                        {
                            wordGroupId = Guid.NewGuid().ToString("N");
                            await InsertGroupAsync(
                                connection,
                                transaction,
                                wordGroupId,
                                item.Group,
                                createdAtUtc,
                                cancellationToken);
                        }

                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            INSERT INTO ImportBatchItems (
                                BatchId, SourceText, AnchorSourceSenseId, WordGroupId, Decision)
                            VALUES (@BatchId, @SourceText, @AnchorSourceSenseId, @WordGroupId, @Decision);
                            """,
                            new
                            {
                                BatchId = batchId,
                                item.SourceText,
                                item.AnchorSourceSenseId,
                                WordGroupId = wordGroupId,
                                Decision = duplicate ? "Duplicate" : "Pending"
                            },
                            transaction,
                            cancellationToken: cancellationToken));
                    }

                    await UpdateBatchStatusAsync(
                        connection,
                        transaction,
                        batchId,
                        createdAtUtc,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
                catch (ImportInboxDataAccessException)
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
                catch (Exception exception)
                {
                    await TryRollbackAsync(transaction);
                    throw new ImportInboxDataAccessException(
                        "写入导入收件箱失败，事务已回滚。",
                        exception);
                }

                return await LoadBatchAsync(connection, batchId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ImportInboxDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ImportInboxDataAccessException(
                    "写入导入收件箱失败。",
                    exception);
            }
        }

        public async Task<IReadOnlyList<ImportBatchPo>> ListPendingAsync(
            string language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language, nameof(language));
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var batchIds = (await connection.QueryAsync<string>(new CommandDefinition(
                    """
                    SELECT BatchId
                    FROM ImportBatches
                    WHERE Language = @Language
                      AND Status = 'Pending'
                      AND EXISTS (
                          SELECT 1
                          FROM ImportBatchItems
                          WHERE ImportBatchItems.BatchId = ImportBatches.BatchId
                            AND Decision = 'Pending')
                    ORDER BY CreatedAtUtc, BatchId;
                    """,
                    new { Language = language },
                    cancellationToken: cancellationToken))).ToArray();

                var batches = new List<ImportBatchPo>(batchIds.Length);
                foreach (var batchId in batchIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    batches.Add(await LoadBatchAsync(connection, batchId, cancellationToken));
                }

                return batches;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ImportInboxDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ImportInboxDataAccessException(
                    "读取导入收件箱失败。",
                    exception);
            }
        }

        public async Task DecideAsync(
            string batchId,
            IReadOnlyList<long> itemIds,
            string decision,
            CancellationToken cancellationToken = default)
        {
            ValidateDecisionRequest(batchId, itemIds, decision);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                try
                {
                    var batchExists = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                        "SELECT 1 FROM ImportBatches WHERE BatchId = @BatchId LIMIT 1;",
                        new { BatchId = batchId },
                        transaction,
                        cancellationToken: cancellationToken));
                    if (batchExists is null)
                    {
                        throw InvalidRequest("导入批次不存在。");
                    }

                    var items = (await connection.QueryAsync<DecisionItemRow>(new CommandDefinition(
                        """
                        SELECT ItemId, WordGroupId, Decision
                        FROM ImportBatchItems
                        WHERE BatchId = @BatchId AND ItemId IN @ItemIds
                        ORDER BY ItemId;
                        """,
                        new { BatchId = batchId, ItemIds = itemIds },
                        transaction,
                        cancellationToken: cancellationToken))).ToArray();
                    if (items.Length != itemIds.Count)
                    {
                        throw InvalidRequest("选择的批次项目不存在或不属于当前批次。");
                    }

                    if (items.Any(item => !string.Equals(item.Decision, "Pending", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(item.WordGroupId)))
                    {
                        throw InvalidRequest("选择的批次项目已处理，不能重复确认。");
                    }

                    foreach (var item in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (string.Equals(decision, "Discard", StringComparison.Ordinal))
                        {
                            await connection.ExecuteAsync(new CommandDefinition(
                                "UPDATE ImportBatchItems SET Decision = @Decision WHERE ItemId = @ItemId;",
                                new { Decision = decision, item.ItemId },
                                transaction,
                                cancellationToken: cancellationToken));
                            await connection.ExecuteAsync(new CommandDefinition(
                                "DELETE FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                                new { item.WordGroupId },
                                transaction,
                                cancellationToken: cancellationToken));
                        }
                        else
                        {
                            await connection.ExecuteAsync(new CommandDefinition(
                                "UPDATE WordGroups SET Placement = @Placement WHERE WordGroupId = @WordGroupId;",
                                new { Placement = decision, item.WordGroupId },
                                transaction,
                                cancellationToken: cancellationToken));
                            await connection.ExecuteAsync(new CommandDefinition(
                                "UPDATE ImportBatchItems SET Decision = @Decision WHERE ItemId = @ItemId;",
                                new { Decision = decision, item.ItemId },
                                transaction,
                                cancellationToken: cancellationToken));
                        }
                    }

                    await UpdateBatchStatusAsync(
                        connection,
                        transaction,
                        batchId,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
                catch (ImportInboxDataAccessException)
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
                catch (Exception exception)
                {
                    await TryRollbackAsync(transaction);
                    throw new ImportInboxDataAccessException(
                        "确认导入收件箱失败，事务已回滚。",
                        exception);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ImportInboxDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ImportInboxDataAccessException(
                    "确认导入收件箱失败。",
                    exception);
            }
        }

        private static async Task<bool> HasExistingMemberAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            string language,
            IReadOnlyList<string> sourceSenseIds,
            CancellationToken cancellationToken)
        {
            var existing = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                """
                SELECT 1
                FROM WordGroupMembers
                INNER JOIN WordGroups ON WordGroups.WordGroupId = WordGroupMembers.WordGroupId
                WHERE WordGroupMembers.Language = @Language
                  AND WordGroups.Placement IN ('Inbox', 'Learning', 'Review')
                  AND WordGroupMembers.SourceSenseId IN @SourceSenseIds
                LIMIT 1;
                """,
                new { Language = language, SourceSenseIds = sourceSenseIds },
                transaction,
                cancellationToken: cancellationToken));
            return existing is not null;
        }

        private static async Task InsertGroupAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            string wordGroupId,
            ImportWordGroupSnapshotPo group,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO WordGroups (
                    WordGroupId, Language, AnchorSourceSenseId, Placement, CreatedAtUtc)
                VALUES (@WordGroupId, @Language, @AnchorSourceSenseId, 'Inbox', @CreatedAtUtc);
                """,
                new
                {
                    WordGroupId = wordGroupId,
                    group.Language,
                    group.AnchorSourceSenseId,
                    CreatedAtUtc = FormatDateTime(createdAtUtc)
                },
                transaction,
                cancellationToken: cancellationToken));

            for (var sortOrder = 0; sortOrder < group.Members.Count; sortOrder++)
            {
                var member = group.Members[sortOrder];
                var memberId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    """
                    INSERT INTO WordGroupMembers (
                        WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                        PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder)
                    VALUES (
                        @WordGroupId, @Language, @SourceSenseId, @SourceLexemeId, @Headword, @Reading,
                        @PartOfSpeechLabel, @AttributeLabel, @IsPhrase, @SortOrder);
                    SELECT last_insert_rowid();
                    """,
                    new
                    {
                        WordGroupId = wordGroupId,
                        member.Language,
                        member.SourceSenseId,
                        member.SourceLexemeId,
                        member.Headword,
                        member.Reading,
                        member.PartOfSpeechLabel,
                        member.AttributeLabel,
                        IsPhrase = member.IsPhrase ? 1 : 0,
                        SortOrder = sortOrder
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                for (var meaningIndex = 0; meaningIndex < member.Meanings.Count; meaningIndex++)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO WordGroupMeanings (MemberId, MeaningIndex, Text) VALUES (@MemberId, @MeaningIndex, @Text);",
                        new { MemberId = memberId, MeaningIndex = meaningIndex, Text = member.Meanings[meaningIndex] },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                for (var formIndex = 0; formIndex < member.Forms.Count; formIndex++)
                {
                    var form = member.Forms[formIndex];
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO WordGroupForms (
                            MemberId, FormIndex, Surface, Reading, FormLabel, DetailLabel, IsCommon, IsPrimary)
                        VALUES (
                            @MemberId, @FormIndex, @Surface, @Reading, @FormLabel, @DetailLabel, @IsCommon, @IsPrimary);
                        """,
                        new
                        {
                            MemberId = memberId,
                            FormIndex = formIndex,
                            form.Surface,
                            form.Reading,
                            form.FormLabel,
                            form.DetailLabel,
                            IsCommon = form.IsCommon ? 1 : 0,
                            IsPrimary = form.IsPrimary ? 1 : 0
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

        }

        private static async Task UpdateBatchStatusAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            string batchId,
            DateTimeOffset completedAtCandidate,
            CancellationToken cancellationToken)
        {
            var pendingCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COUNT(*) FROM ImportBatchItems WHERE BatchId = @BatchId AND Decision = 'Pending';",
                new { BatchId = batchId },
                transaction,
                cancellationToken: cancellationToken));
            var completed = pendingCount == 0;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE ImportBatches
                SET Status = @Status, CompletedAtUtc = @CompletedAtUtc
                WHERE BatchId = @BatchId;
                """,
                new
                {
                    BatchId = batchId,
                    Status = completed ? "Completed" : "Pending",
                    CompletedAtUtc = completed ? FormatDateTime(completedAtCandidate) : null
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        private static async Task<ImportBatchPo> LoadBatchAsync(
            SqliteConnection connection,
            string batchId,
            CancellationToken cancellationToken)
        {
            var batch = await connection.QuerySingleOrDefaultAsync<BatchRow>(new CommandDefinition(
                "SELECT BatchId, Language, SourceKind, Status, CreatedAtUtc, CompletedAtUtc FROM ImportBatches WHERE BatchId = @BatchId;",
                new { BatchId = batchId },
                cancellationToken: cancellationToken));
            if (batch is null)
            {
                throw InvalidRequest("导入批次不存在。");
            }

            var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition(
                """
                SELECT ItemId, SourceText, AnchorSourceSenseId, WordGroupId, Decision
                FROM ImportBatchItems
                WHERE BatchId = @BatchId
                ORDER BY ItemId;
                """,
                new { BatchId = batchId },
                cancellationToken: cancellationToken))).ToArray();
            var groupIds = items
                .Where(item => !string.IsNullOrWhiteSpace(item.WordGroupId))
                .Select(item => item.WordGroupId!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var groups = groupIds.Length == 0
                ? Array.Empty<GroupRow>()
                : (await connection.QueryAsync<GroupRow>(new CommandDefinition(
                    """
                    SELECT WordGroupId, Language, AnchorSourceSenseId, Placement, CreatedAtUtc
                    FROM WordGroups
                    WHERE WordGroupId IN @GroupIds;
                    """,
                    new { GroupIds = groupIds },
                    cancellationToken: cancellationToken))).ToArray();
            var members = groupIds.Length == 0
                ? Array.Empty<MemberRow>()
                : (await connection.QueryAsync<MemberRow>(new CommandDefinition(
                    """
                    SELECT MemberId, WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                           PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder
                    FROM WordGroupMembers
                    WHERE WordGroupId IN @GroupIds
                    ORDER BY WordGroupId, SortOrder;
                    """,
                    new { GroupIds = groupIds },
                    cancellationToken: cancellationToken))).ToArray();
            var memberIds = members.Select(member => member.MemberId).ToArray();
            var meanings = memberIds.Length == 0
                ? Array.Empty<MeaningRow>()
                : (await connection.QueryAsync<MeaningRow>(new CommandDefinition(
                    """
                    SELECT MemberId, MeaningIndex, Text
                    FROM WordGroupMeanings
                    WHERE MemberId IN @MemberIds
                    ORDER BY MemberId, MeaningIndex;
                    """,
                    new { MemberIds = memberIds },
                    cancellationToken: cancellationToken))).ToArray();
            var forms = memberIds.Length == 0
                ? Array.Empty<FormRow>()
                : (await connection.QueryAsync<FormRow>(new CommandDefinition(
                    """
                    SELECT MemberId, FormIndex, Surface, Reading, FormLabel, DetailLabel, IsCommon, IsPrimary
                    FROM WordGroupForms
                    WHERE MemberId IN @MemberIds
                    ORDER BY MemberId, FormIndex;
                    """,
                    new { MemberIds = memberIds },
                    cancellationToken: cancellationToken))).ToArray();

            var groupById = groups.ToDictionary(group => group.WordGroupId, StringComparer.Ordinal);
            var membersByGroup = members
                .GroupBy(member => member.WordGroupId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
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
                        .Select(form => new ImportWordGroupFormPo(
                            form.Surface,
                            form.Reading,
                            form.FormLabel,
                            form.DetailLabel,
                            form.IsCommon != 0,
                            form.IsPrimary != 0))
                        .ToArray());
            var groupPoById = new Dictionary<string, ImportWordGroupPo>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var groupMembers = membersByGroup.TryGetValue(group.WordGroupId, out var groupMemberRows)
                    ? groupMemberRows
                    : Array.Empty<MemberRow>();
                groupPoById[group.WordGroupId] = new ImportWordGroupPo(
                    group.WordGroupId,
                    group.Language,
                    group.AnchorSourceSenseId,
                    group.Placement,
                    ParseDateTime(group.CreatedAtUtc, "WordGroups.CreatedAtUtc"),
                    groupMembers
                        .OrderBy(member => member.SortOrder)
                        .Select(member => new ImportWordGroupMemberPo(
                            member.Language,
                            member.SourceSenseId,
                            member.SourceLexemeId,
                            member.Headword,
                            member.Reading,
                            member.PartOfSpeechLabel,
                            member.AttributeLabel,
                            member.IsPhrase != 0,
                            meaningsByMember.TryGetValue(member.MemberId, out var memberMeanings)
                                ? memberMeanings
                                : Array.Empty<string>(),
                            formsByMember.TryGetValue(member.MemberId, out var memberForms)
                                ? memberForms
                                : Array.Empty<ImportWordGroupFormPo>()))
                        .ToArray());
            }

            return new ImportBatchPo(
                batch.BatchId,
                batch.Language,
                batch.SourceKind,
                batch.Status,
                ParseDateTime(batch.CreatedAtUtc, "ImportBatches.CreatedAtUtc"),
                batch.CompletedAtUtc is null
                    ? null
                    : ParseDateTime(batch.CompletedAtUtc, "ImportBatches.CompletedAtUtc"),
                items.Select(item => new ImportBatchItemPo(
                    item.ItemId,
                    item.SourceText,
                    item.AnchorSourceSenseId,
                    item.WordGroupId,
                    item.Decision,
                    item.WordGroupId is not null && groupPoById.TryGetValue(item.WordGroupId, out var group)
                        ? group
                        : null)).ToArray());
        }

        private static void ValidateStageRequest(ImportBatchStagePo request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLanguage(request.Language, nameof(request.Language));
            ValidateSourceKind(request.SourceKind, nameof(request.SourceKind));
            if (request.Items is null || request.Items.Count == 0)
            {
                throw new ArgumentException("导入批次至少需要一个项目。", nameof(request.Items));
            }

            foreach (var item in request.Items)
            {
                if (item is null)
                {
                    throw new ArgumentException("导入批次项目不能为空。", nameof(request.Items));
                }

                RequireText(item.SourceText, "批次项目来源词形");
                RequireText(item.AnchorSourceSenseId, "批次项目来源义项");
                if (item.Group is null)
                {
                    throw new ArgumentException("批次项目词组不能为空。", nameof(request.Items));
                }

                ValidateGroup(request.Language, item.AnchorSourceSenseId, item.Group);
            }
        }

        private static void ValidateGroup(
            string requestLanguage,
            string itemAnchorSourceSenseId,
            ImportWordGroupSnapshotPo group)
        {
            ValidateLanguage(group.Language, "group.Language");
            if (!string.Equals(group.Language, requestLanguage, StringComparison.Ordinal))
            {
                throw new ArgumentException("词组语言必须与导入批次一致。", nameof(group));
            }

            RequireText(group.AnchorSourceSenseId, "group.AnchorSourceSenseId");
            if (!string.Equals(group.AnchorSourceSenseId, itemAnchorSourceSenseId, StringComparison.Ordinal))
            {
                throw new ArgumentException("批次项目锚点必须与词组锚点一致。", nameof(group));
            }

            if (group.Members is null || group.Members.Count == 0)
            {
                throw new ArgumentException("词组至少需要一个成员。", nameof(group.Members));
            }

            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in group.Members)
            {
                if (member is null)
                {
                    throw new ArgumentException("词组成员不能为空。", nameof(group.Members));
                }

                ValidateLanguage(member.Language, "member.Language");
                if (!string.Equals(member.Language, requestLanguage, StringComparison.Ordinal))
                {
                    throw new ArgumentException("词组成员语言必须一致。", nameof(group.Members));
                }

                RequireText(member.SourceSenseId, "member.SourceSenseId");
                if (!memberIds.Add(member.SourceSenseId))
                {
                    throw new ArgumentException("词组不能包含重复来源义项。", nameof(group.Members));
                }

                RequireText(member.SourceLexemeId, "member.SourceLexemeId");
                RequireText(member.Headword, "member.Headword");
                RequireText(member.PartOfSpeechLabel, "member.PartOfSpeechLabel");
                RequireText(member.AttributeLabel, "member.AttributeLabel");
                if (member.Meanings is null || member.Forms is null)
                {
                    throw new ArgumentException("词组成员释义和词形不能为空。", nameof(group.Members));
                }

                foreach (var meaning in member.Meanings)
                {
                    RequireText(meaning, "member.Meanings");
                }

                foreach (var form in member.Forms)
                {
                    if (form is null)
                    {
                        throw new ArgumentException("词组词形不能为空。", nameof(member.Forms));
                    }

                    RequireText(form.Surface, "form.Surface");
                    RequireText(form.FormLabel, "form.FormLabel");
                    RequireText(form.DetailLabel, "form.DetailLabel");
                }
            }

            if (!memberIds.Contains(group.AnchorSourceSenseId))
            {
                throw new ArgumentException("词组锚点必须存在于成员中。", nameof(group));
            }

        }

        private static void ValidateDecisionRequest(
            string batchId,
            IReadOnlyList<long> itemIds,
            string decision)
        {
            RequireText(batchId, nameof(batchId));
            if (itemIds is null || itemIds.Count == 0)
            {
                throw new ArgumentException("至少选择一个批次项目。", nameof(itemIds));
            }

            if (itemIds.Any(itemId => itemId <= 0)
                || itemIds.Distinct().Count() != itemIds.Count)
            {
                throw new ArgumentException("批次项目编号必须是正数且不能重复。", nameof(itemIds));
            }

            if (!Decisions.Contains(decision))
            {
                throw new ArgumentException("导入决定必须是 Learning、Review 或 Discard。", nameof(decision));
            }
        }

        private static void ValidateLanguage(string language, string parameterName)
        {
            RequireText(language, parameterName);
            if (!Languages.Contains(language))
            {
                throw new ArgumentException("语言必须是 Japanese 或 English。", parameterName);
            }
        }

        private static void ValidateSourceKind(string sourceKind, string parameterName)
        {
            RequireText(sourceKind, parameterName);
            if (!SourceKinds.Contains(sourceKind))
            {
                throw new ArgumentException("导入来源必须是 Manual、Text 或 Mcp。", parameterName);
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("文本不能为空。", parameterName)
                : value;
        }

        private static ImportInboxDataAccessException InvalidRequest(string message)
        {
            return new ImportInboxDataAccessException(message);
        }

        private static string FormatDateTime(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTimeOffset ParseDateTime(string value, string fieldName)
        {
            if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
            {
                throw new FormatException($"{fieldName} 不是有效的 UTC 时间。");
            }

            return result;
        }

        private static async Task TryRollbackAsync(System.Data.Common.DbTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // 保留原始收件箱异常，连接释放时会终止未完成的事务。
            }
        }

        private sealed class BatchRow
        {
            public string BatchId { get; set; } = string.Empty;

            public string Language { get; set; } = string.Empty;

            public string SourceKind { get; set; } = string.Empty;

            public string Status { get; set; } = string.Empty;

            public string CreatedAtUtc { get; set; } = string.Empty;

            public string? CompletedAtUtc { get; set; }
        }

        private sealed class ItemRow
        {
            public long ItemId { get; set; }

            public string SourceText { get; set; } = string.Empty;

            public string AnchorSourceSenseId { get; set; } = string.Empty;

            public string? WordGroupId { get; set; }

            public string Decision { get; set; } = string.Empty;
        }

        private sealed class DecisionItemRow
        {
            public long ItemId { get; set; }

            public string? WordGroupId { get; set; }

            public string Decision { get; set; } = string.Empty;
        }

        private sealed class GroupRow
        {
            public string WordGroupId { get; set; } = string.Empty;

            public string Language { get; set; } = string.Empty;

            public string AnchorSourceSenseId { get; set; } = string.Empty;

            public string Placement { get; set; } = string.Empty;

            public string CreatedAtUtc { get; set; } = string.Empty;
        }

        private sealed class MemberRow
        {
            public long MemberId { get; set; }

            public string WordGroupId { get; set; } = string.Empty;

            public string Language { get; set; } = string.Empty;

            public string SourceSenseId { get; set; } = string.Empty;

            public string SourceLexemeId { get; set; } = string.Empty;

            public string Headword { get; set; } = string.Empty;

            public string? Reading { get; set; }

            public string PartOfSpeechLabel { get; set; } = string.Empty;

            public string AttributeLabel { get; set; } = string.Empty;

            public int IsPhrase { get; set; }

            public int SortOrder { get; set; }
        }

        private sealed class MeaningRow
        {
            public long MemberId { get; set; }

            public int MeaningIndex { get; set; }

            public string Text { get; set; } = string.Empty;
        }

        private sealed class FormRow
        {
            public long MemberId { get; set; }

            public int FormIndex { get; set; }

            public string Surface { get; set; } = string.Empty;

            public string? Reading { get; set; }

            public string FormLabel { get; set; } = string.Empty;

            public string DetailLabel { get; set; } = string.Empty;

            public int IsCommon { get; set; }

            public int IsPrimary { get; set; }
        }
    }
}
