using Dapper;

namespace Dictation.DataAccess.Lexicon
{
    public sealed class EnglishLexiconDataAccessor
    {
        private readonly LexiconSqliteConnectionFactory connectionFactory;

        public EnglishLexiconDataAccessor(LexiconSqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<IReadOnlyList<EnglishLexiconLookupPo>> SearchAsync(
            string normalizedQuery,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 50);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateEnglishConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "英语",
                    cancellationToken);

                var escapedQuery = LexiconSqliteQuerySupport.EscapeLikePattern(normalizedQuery);
                var lexemeIds = (await connection.QueryAsync<string>(new CommandDefinition(
                    """
                    SELECT LexemeId
                    FROM Forms
                    WHERE Surface = @Query
                       OR Surface LIKE @Prefix ESCAPE '\'
                    GROUP BY LexemeId
                    ORDER BY MIN(CASE WHEN Surface = @Query THEN 0 ELSE 1 END), LexemeId
                    LIMIT @Limit;
                    """,
                    new
                    {
                        Query = normalizedQuery,
                        Prefix = escapedQuery + "%",
                        Limit = limit
                    },
                    cancellationToken: cancellationToken))).ToArray();
                if (lexemeIds.Length == 0)
                {
                    return Array.Empty<EnglishLexiconLookupPo>();
                }

                var parameters = new { LexemeIds = lexemeIds };
                var senses = (await connection.QueryAsync<SenseRow>(new CommandDefinition(
                    """
                    SELECT SenseId, LexemeId, SynsetId, SenseOrder
                    FROM Senses
                    WHERE LexemeId IN @LexemeIds
                    ORDER BY LexemeId, SenseOrder, SenseId;
                    """,
                    parameters,
                    cancellationToken: cancellationToken))).ToArray();
                return await LoadSensesAsync(
                    connection,
                    senses,
                    senses.Select(sense => sense.SenseId).ToArray(),
                    limit,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LexiconDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw LexiconSqliteQuerySupport.WrapQueryFailure("英语", exception);
            }
        }

        public async Task<IReadOnlyList<EnglishLexiconLookupPo>> GetSenseGroupAsync(
            string sourceSenseId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceSenseId);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateEnglishConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "英语",
                    cancellationToken);

                var anchor = await connection.QuerySingleOrDefaultAsync<AnchorRow>(new CommandDefinition(
                    """
                    SELECT Senses.SenseId, Lexemes.IsPhrase
                    FROM Senses
                    INNER JOIN Lexemes ON Lexemes.LexemeId = Senses.LexemeId
                    WHERE Senses.SenseId = @SourceSenseId;
                    """,
                    new { SourceSenseId = sourceSenseId },
                    cancellationToken: cancellationToken));
                if (anchor is null)
                {
                    return Array.Empty<EnglishLexiconLookupPo>();
                }

                var senseIds = new List<string> { sourceSenseId };
                if (anchor.IsPhrase == 0)
                {
                    var neighborIds = (await connection.QueryAsync<string>(new CommandDefinition(
                        """
                        SELECT CASE
                            WHEN Derivations.SourceSenseId = @SourceSenseId THEN Derivations.TargetSenseId
                            ELSE Derivations.SourceSenseId
                        END
                        FROM Derivations
                        INNER JOIN Senses AS Neighbor
                            ON Neighbor.SenseId = CASE
                                WHEN Derivations.SourceSenseId = @SourceSenseId THEN Derivations.TargetSenseId
                                ELSE Derivations.SourceSenseId
                            END
                        INNER JOIN Lexemes AS NeighborLexeme ON NeighborLexeme.LexemeId = Neighbor.LexemeId
                        WHERE (Derivations.SourceSenseId = @SourceSenseId
                            OR Derivations.TargetSenseId = @SourceSenseId)
                          AND NeighborLexeme.IsPhrase = 0
                        ORDER BY Neighbor.SenseOrder, Neighbor.SenseId;
                        """,
                        new { SourceSenseId = sourceSenseId },
                        cancellationToken: cancellationToken))).ToArray();
                    foreach (var neighborId in neighborIds)
                    {
                        if (!senseIds.Contains(neighborId, StringComparer.Ordinal))
                        {
                            senseIds.Add(neighborId);
                        }
                    }
                }

                var senses = (await connection.QueryAsync<SenseRow>(new CommandDefinition(
                    """
                    SELECT SenseId, LexemeId, SynsetId, SenseOrder
                    FROM Senses
                    WHERE SenseId IN @SenseIds;
                    """,
                    new { SenseIds = senseIds },
                    cancellationToken: cancellationToken))).ToArray();
                return await LoadSensesAsync(
                    connection,
                    senses,
                    senseIds,
                    null,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LexiconDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw LexiconSqliteQuerySupport.WrapQueryFailure("英语", exception);
            }
        }

        private static async Task<IReadOnlyList<EnglishLexiconLookupPo>> LoadSensesAsync(
            Microsoft.Data.Sqlite.SqliteConnection connection,
            IReadOnlyList<SenseRow> senses,
            IReadOnlyList<string> orderedSenseIds,
            int? limit,
            CancellationToken cancellationToken)
        {
            if (senses.Count == 0)
            {
                return Array.Empty<EnglishLexiconLookupPo>();
            }

            var lexemeIds = senses
                .Select(sense => sense.LexemeId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var parameters = new { LexemeIds = lexemeIds };
            var lexemes = (await connection.QueryAsync<LexemeRow>(new CommandDefinition(
                """
                SELECT LexemeId, Lemma, SourceEntryKey, PartOfSpeech, IsPhrase
                FROM Lexemes
                WHERE LexemeId IN @LexemeIds;
                """,
                parameters,
                cancellationToken: cancellationToken))).ToDictionary(
                row => row.LexemeId,
                StringComparer.Ordinal);
            var synsetIds = senses
                .Select(sense => sense.SynsetId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var definitions = (await connection.QueryAsync<DefinitionRow>(new CommandDefinition(
                """
                SELECT SynsetId, DefinitionIndex, Text
                FROM Definitions
                WHERE SynsetId IN @SynsetIds
                ORDER BY SynsetId, DefinitionIndex;
                """,
                new { SynsetIds = synsetIds },
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.SynsetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(row => row.Text).ToArray(),
                    StringComparer.Ordinal);
            var forms = (await connection.QueryAsync<FormRow>(new CommandDefinition(
                """
                SELECT LexemeId, FormId, Surface, FormKind, FormSourceKind
                FROM Forms
                WHERE LexemeId IN @LexemeIds
                ORDER BY LexemeId, FormId;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.LexemeId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<EnglishLexiconLookupFormPo>)group
                        .Select(row => new EnglishLexiconLookupFormPo(
                            row.Surface,
                            row.FormKind,
                            row.FormSourceKind))
                        .ToArray(),
                    StringComparer.Ordinal);
            var sensesById = senses.ToDictionary(sense => sense.SenseId, StringComparer.Ordinal);
            var result = new List<EnglishLexiconLookupPo>();
            foreach (var senseId in orderedSenseIds)
            {
                if (!sensesById.TryGetValue(senseId, out var sense)
                    || !lexemes.TryGetValue(sense.LexemeId, out var lexeme))
                {
                    continue;
                }

                var lexemeForms = forms.TryGetValue(lexeme.LexemeId, out var formsForLexeme)
                    ? formsForLexeme
                    : Array.Empty<EnglishLexiconLookupFormPo>();
                result.Add(new EnglishLexiconLookupPo(
                    sense.SenseId,
                    lexeme.LexemeId,
                    lexeme.Lemma,
                    lexeme.SourceEntryKey,
                    lexeme.PartOfSpeech,
                    lexeme.IsPhrase != 0,
                    definitions.TryGetValue(sense.SynsetId, out var senseDefinitions)
                        ? senseDefinitions
                        : Array.Empty<string>(),
                    lexemeForms));
                if (limit is not null && result.Count >= limit.Value)
                {
                    break;
                }
            }

            return result;
        }

        private sealed class LexemeRow
        {
            public string LexemeId { get; set; } = string.Empty;

            public string Lemma { get; set; } = string.Empty;

            public string SourceEntryKey { get; set; } = string.Empty;

            public string PartOfSpeech { get; set; } = string.Empty;

            public int IsPhrase { get; set; }
        }

        private sealed class AnchorRow
        {
            public string SenseId { get; set; } = string.Empty;

            public int IsPhrase { get; set; }
        }

        private sealed class SenseRow
        {
            public string SenseId { get; set; } = string.Empty;

            public string LexemeId { get; set; } = string.Empty;

            public string SynsetId { get; set; } = string.Empty;

            public int SenseOrder { get; set; }
        }

        private sealed class DefinitionRow
        {
            public string SynsetId { get; set; } = string.Empty;

            public int DefinitionIndex { get; set; }

            public string Text { get; set; } = string.Empty;
        }

        private sealed class FormRow
        {
            public string LexemeId { get; set; } = string.Empty;

            public long FormId { get; set; }

            public string Surface { get; set; } = string.Empty;

            public string FormKind { get; set; } = string.Empty;

            public string FormSourceKind { get; set; } = string.Empty;
        }
    }
}
