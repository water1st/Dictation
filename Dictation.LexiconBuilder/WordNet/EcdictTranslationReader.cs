using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Data.Sqlite;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 从只读 ECDICT 数据库提取按词性匹配的中文翻译。
    /// </summary>
    internal sealed class EcdictTranslationReader : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly SqliteCommand lookupCommand;
        private readonly Dictionary<string, IReadOnlyList<EcdictRow>> rowsByLemma = new(StringComparer.Ordinal);
        private bool disposed;

        /// <summary>
        /// 从只读 ECDICT 数据库提取按词性匹配的中文翻译。
        /// </summary>
        /// <param name="databasePath">只读中文来源 SQLite 数据库路径。</param>
        internal EcdictTranslationReader(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("ECDICT 数据库路径不能为空。", nameof(databasePath));
            }

            var fullPath = Path.GetFullPath(databasePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"找不到 ECDICT 数据库：{fullPath}。", fullPath);
            }

            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());

            try
            {
                connection.Open();
                lookupCommand = connection.CreateCommand();
                lookupCommand.CommandText = "SELECT translation, pos FROM stardict WHERE word = $word ORDER BY rowid;";
                lookupCommand.Parameters.Add("$word", SqliteType.Text);
                lookupCommand.Prepare();
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 按规范化词元和词性读取中文翻译，过滤不对应的词性段。
        /// </summary>
        /// <param name="normalizedLemma">用于来源查找的规范化词元。</param>
        /// <param name="partOfSpeech">用于词形或翻译分类的英语词性。</param>
        internal IReadOnlyList<string> Read(
            string normalizedLemma,
            EnglishPartOfSpeech partOfSpeech)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (string.IsNullOrWhiteSpace(normalizedLemma))
            {
                throw new ArgumentException("ECDICT 查询词不能为空。", nameof(normalizedLemma));
            }

            var prefixes = GetPrefixes(partOfSpeech);
            var lemma = NormalizeLemma(normalizedLemma);
            if (!rowsByLemma.TryGetValue(lemma, out var rows))
            {
                rows = ReadRows(lemma);
                rowsByLemma.Add(lemma, rows);
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                foreach (var line in row.Translation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var meaning = TryReadMeaning(line, prefixes);
                    if (meaning is not null && seen.Add(meaning))
                    {
                        result.Add(meaning);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lookupCommand.Dispose();
            connection.Dispose();
        }

        private IReadOnlyList<EcdictRow> ReadRows(string lemma)
        {
            lookupCommand.Parameters["$word"].Value = lemma;
            using var reader = lookupCommand.ExecuteReader();
            var rows = new List<EcdictRow>();
            while (reader.Read())
            {
                var translation = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var partOfSpeech = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                rows.Add(new EcdictRow(translation, partOfSpeech));
            }

            return rows;
        }

        private static IReadOnlyList<string> GetPrefixes(EnglishPartOfSpeech partOfSpeech)
        {
            return partOfSpeech switch
            {
                EnglishPartOfSpeech.Noun => new[] { "n." },
                EnglishPartOfSpeech.Verb => new[] { "v.", "vi.", "vt." },
                EnglishPartOfSpeech.Adjective or EnglishPartOfSpeech.AdjectiveSatellite => new[] { "a.", "adj." },
                EnglishPartOfSpeech.Adverb => new[] { "ad.", "adv." },
                _ => throw new ArgumentOutOfRangeException(nameof(partOfSpeech), partOfSpeech, "ECDICT 不支持该词性。")
            };
        }

        private static string? TryReadMeaning(string line, IReadOnlyList<string> prefixes)
        {
            var trimmedLine = line.Trim();
            foreach (var prefix in prefixes)
            {
                if (!trimmedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var meaning = trimmedLine[prefix.Length..].Trim();
                return meaning.Length == 0
                    ? null
                    : meaning.Normalize(NormalizationForm.FormC);
            }

            return null;
        }

        private static string NormalizeLemma(string value)
        {
            return value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private sealed record EcdictRow(string Translation, string PartOfSpeech);
    }
}
