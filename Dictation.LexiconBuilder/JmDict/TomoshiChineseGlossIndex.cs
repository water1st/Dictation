using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Dictation.LexiconBuilder.JmDict
{
    internal sealed class TomoshiChineseGlossIndex
    {
        private readonly IReadOnlyDictionary<(long EntryId, int ZeroBasedSenseIndex), IReadOnlyList<string>> meanings;

        private TomoshiChineseGlossIndex(
            IReadOnlyDictionary<(long EntryId, int ZeroBasedSenseIndex), IReadOnlyList<string>> meanings)
        {
            this.meanings = meanings;
        }

        internal static TomoshiChineseGlossIndex Load(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            var values = new Dictionary<(long EntryId, int ZeroBasedSenseIndex), IReadOnlyList<string>>();
            try
            {
                using var connection = new SqliteConnection(
                    $"Data Source={Path.GetFullPath(databasePath)};Mode=ReadOnly;Pooling=False");
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT entry_id, data
                    FROM zh_defs
                    WHERE locale = 'zh-CN'
                    ORDER BY entry_id;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var entryIdText = reader.GetString(0);
                    if (!long.TryParse(entryIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var entryId))
                    {
                        throw new LexiconBuildException(
                            $"Tomoshi zh_defs 包含无效 entry_id：{entryIdText}。");
                    }

                    ReadEntry(values, entryId, reader.GetString(1));
                }
            }
            catch (LexiconBuildException)
            {
                throw;
            }
            catch (Exception exception) when (exception is SqliteException or IOException)
            {
                throw new LexiconBuildException("读取 Tomoshi 中文词库失败。", exception);
            }

            return new TomoshiChineseGlossIndex(values);
        }

        internal IReadOnlyList<string> GetMeanings(long jmDictEntryId, int oneBasedSenseIndex)
        {
            if (oneBasedSenseIndex <= 0)
            {
                throw new LexiconBuildException("Tomoshi 义项序号必须为正数。");
            }

            return meanings.TryGetValue((jmDictEntryId, oneBasedSenseIndex - 1), out var result)
                ? result
                : Array.Empty<string>();
        }

        private static void ReadEntry(
            IDictionary<(long EntryId, int ZeroBasedSenseIndex), IReadOnlyList<string>> values,
            long entryId,
            string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("senses", out var senses)
                    || senses.ValueKind != JsonValueKind.Object)
                {
                    throw new LexiconBuildException(
                        $"Tomoshi entry {entryId} 缺少有效 senses 对象。");
                }

                foreach (var sense in senses.EnumerateObject())
                {
                    if (!int.TryParse(
                            sense.Name,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var zeroBasedSenseIndex)
                        || zeroBasedSenseIndex < 0)
                    {
                        throw new LexiconBuildException(
                            $"Tomoshi entry {entryId} 包含无效义项键：{sense.Name}。");
                    }

                    values[(entryId, zeroBasedSenseIndex)] = ReadGlosses(sense.Value);
                }
            }
            catch (LexiconBuildException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new LexiconBuildException(
                    $"Tomoshi entry {entryId} 的中文 JSON 无效。",
                    exception);
            }
        }

        private static IReadOnlyList<string> ReadGlosses(JsonElement sense)
        {
            if (!sense.TryGetProperty("glosses", out var glosses)
                || glosses.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gloss in glosses.EnumerateArray())
            {
                if (!gloss.TryGetProperty("text", out var textElement)
                    || textElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var text = textElement.GetString()?.Trim().Normalize(NormalizationForm.FormC);
                if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                {
                    result.Add(text);
                }
            }

            return result;
        }
    }
}
