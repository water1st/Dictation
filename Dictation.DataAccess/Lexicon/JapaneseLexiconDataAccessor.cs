using Dapper;

namespace Dictation.DataAccess.Lexicon
{
    public sealed class JapaneseLexiconDataAccessor
    {
        private readonly LexiconSqliteConnectionFactory connectionFactory;

        public JapaneseLexiconDataAccessor(LexiconSqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<IReadOnlyList<JapaneseLexiconLookupPo>> SearchAsync(
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
                await using var connection = connectionFactory.CreateJapaneseConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "日语",
                    cancellationToken);

                var escapedQuery = LexiconSqliteQuerySupport.EscapeLikePattern(normalizedQuery);
                var entryIds = (await connection.QueryAsync<string>(new CommandDefinition(
                    """
                    SELECT Forms.EntryId
                    FROM Forms
                    INNER JOIN Entries ON Entries.EntryId = Forms.EntryId
                    WHERE Forms.Surface = @Query
                       OR Forms.Surface LIKE @Prefix ESCAPE '\'
                    GROUP BY Forms.EntryId, Entries.SourceSequence, Entries.SenseIndex
                    ORDER BY MIN(CASE WHEN Forms.Surface = @Query THEN 0 ELSE 1 END),
                             Entries.SourceSequence, Entries.SenseIndex, Forms.EntryId
                    LIMIT @Limit;
                    """,
                    new
                    {
                        Query = normalizedQuery,
                        Prefix = escapedQuery + "%",
                        Limit = limit
                    },
                    cancellationToken: cancellationToken))).ToArray();
                if (entryIds.Length == 0)
                {
                    return Array.Empty<JapaneseLexiconLookupPo>();
                }

                var parameters = new { EntryIds = entryIds };
                var entries = (await connection.QueryAsync<EntryRow>(new CommandDefinition(
                    """
                    SELECT EntryId, PrimaryWritten, PrimaryReading, LexicalCategory,
                           VerbClass, Transitivity, KanjiUsage, IsPhrase
                    FROM Entries
                    WHERE EntryId IN @EntryIds;
                    """,
                    parameters,
                    cancellationToken: cancellationToken))).ToDictionary(
                    row => row.EntryId,
                    StringComparer.Ordinal);
                var partsOfSpeech = (await connection.QueryAsync<PartOfSpeechRow>(new CommandDefinition(
                    """
                    SELECT EntryId, SortIndex, Value
                    FROM PartsOfSpeech
                    WHERE EntryId IN @EntryIds
                    ORDER BY EntryId, SortIndex;
                    """,
                    parameters,
                    cancellationToken: cancellationToken)))
                    .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<string>)group.Select(row => row.Value).ToArray(),
                        StringComparer.Ordinal);
                var meanings = (await connection.QueryAsync<MeaningRow>(new CommandDefinition(
                    """
                    SELECT EntryId, Text
                    FROM Glosses
                    WHERE EntryId IN @EntryIds AND Language = 'eng'
                    ORDER BY EntryId, GlossIndex;
                    """,
                    parameters,
                    cancellationToken: cancellationToken)))
                    .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<string>)group.Select(row => row.Text).ToArray(),
                        StringComparer.Ordinal);
                var forms = (await connection.QueryAsync<FormRow>(new CommandDefinition(
                    """
                    SELECT EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole, IsCommon, IsPrimary
                    FROM Forms
                    WHERE EntryId IN @EntryIds
                    ORDER BY EntryId, FormId;
                    """,
                    parameters,
                    cancellationToken: cancellationToken)))
                    .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<JapaneseLexiconLookupFormPo>)group
                            .Select(row => new JapaneseLexiconLookupFormPo(
                                row.Surface,
                                row.Reading,
                                row.FormKind,
                                row.ScriptKind,
                                row.LexicalRole,
                                row.IsCommon != 0,
                                row.IsPrimary != 0))
                            .ToArray(),
                        StringComparer.Ordinal);

                return entryIds
                    .Where(entries.ContainsKey)
                    .Select(entryId =>
                    {
                        var entry = entries[entryId];
                        return new JapaneseLexiconLookupPo(
                            entry.EntryId,
                            entry.PrimaryWritten,
                            entry.PrimaryReading,
                            entry.LexicalCategory,
                            entry.VerbClass,
                            entry.Transitivity,
                            entry.KanjiUsage,
                            entry.IsPhrase != 0,
                            partsOfSpeech.TryGetValue(entryId, out var entryPartsOfSpeech)
                                ? entryPartsOfSpeech
                                : Array.Empty<string>(),
                            meanings.TryGetValue(entryId, out var entryMeanings)
                                ? entryMeanings
                                : Array.Empty<string>(),
                            forms.TryGetValue(entryId, out var entryForms)
                                ? entryForms
                                : Array.Empty<JapaneseLexiconLookupFormPo>());
                    })
                    .ToArray();
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
                throw LexiconSqliteQuerySupport.WrapQueryFailure("日语", exception);
            }
        }

        private sealed class EntryRow
        {
            public string EntryId { get; set; } = string.Empty;

            public string PrimaryWritten { get; set; } = string.Empty;

            public string PrimaryReading { get; set; } = string.Empty;

            public string LexicalCategory { get; set; } = string.Empty;

            public string VerbClass { get; set; } = string.Empty;

            public string Transitivity { get; set; } = string.Empty;

            public string KanjiUsage { get; set; } = string.Empty;

            public int IsPhrase { get; set; }
        }

        private sealed class PartOfSpeechRow
        {
            public string EntryId { get; set; } = string.Empty;

            public int SortIndex { get; set; }

            public string Value { get; set; } = string.Empty;
        }

        private sealed class MeaningRow
        {
            public string EntryId { get; set; } = string.Empty;

            public int GlossIndex { get; set; }

            public string Text { get; set; } = string.Empty;
        }

        private sealed class FormRow
        {
            public string EntryId { get; set; } = string.Empty;

            public long FormId { get; set; }

            public string Surface { get; set; } = string.Empty;

            public string Reading { get; set; } = string.Empty;

            public string FormKind { get; set; } = string.Empty;

            public string ScriptKind { get; set; } = string.Empty;

            public string LexicalRole { get; set; } = string.Empty;

            public int IsCommon { get; set; }

            public int IsPrimary { get; set; }
        }
    }
}
