using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.WordNet
{
    internal sealed class ChineseOpenWordNetParser
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ParseByIli(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath))
            {
                throw new ArgumentException("Chinese Open WordNet 路径不能为空。", nameof(xmlPath));
            }

            var fullPath = Path.GetFullPath(xmlPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"找不到 Chinese Open WordNet 文件：{fullPath}。", fullPath);
            }

            var meaningByMemberId = ReadMemberMeanings(fullPath);
            return ReadSynsetMeanings(fullPath, meaningByMemberId);
        }

        private static IReadOnlyDictionary<string, string> ReadMemberMeanings(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = XmlReader.Create(path, CreateSettings());
            string? currentLemma = null;
            var lexicalEntryDepth = -1;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element
                    && string.Equals(reader.LocalName, "LexicalEntry", StringComparison.Ordinal))
                {
                    currentLemma = null;
                    lexicalEntryDepth = reader.Depth;
                    continue;
                }

                if (lexicalEntryDepth >= 0
                    && reader.NodeType == XmlNodeType.Element
                    && string.Equals(reader.LocalName, "Lemma", StringComparison.Ordinal))
                {
                    currentLemma = Normalize(reader.GetAttribute("writtenForm"));
                    continue;
                }

                if (lexicalEntryDepth >= 0
                    && reader.NodeType == XmlNodeType.Element
                    && string.Equals(reader.LocalName, "Sense", StringComparison.Ordinal))
                {
                    var memberId = Normalize(reader.GetAttribute("id"));
                    if (memberId is null || currentLemma is null)
                    {
                        throw new LexiconBuildException($"Chinese Open WordNet 文件 {path} 含有缺少 id 或词面的 Sense。");
                    }

                    if (result.TryGetValue(memberId, out var existingMeaning))
                    {
                        if (!string.Equals(existingMeaning, currentLemma, StringComparison.Ordinal))
                        {
                            throw new LexiconBuildException(
                                $"Chinese Open WordNet 文件 {path} 的 member {memberId} 对应多个不同词面。");
                        }

                        continue;
                    }

                    result.Add(memberId, currentLemma);
                    continue;
                }

                if (reader.NodeType == XmlNodeType.EndElement
                    && reader.Depth == lexicalEntryDepth
                    && string.Equals(reader.LocalName, "LexicalEntry", StringComparison.Ordinal))
                {
                    currentLemma = null;
                    lexicalEntryDepth = -1;
                }
            }

            return result;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadSynsetMeanings(
            string path,
            IReadOnlyDictionary<string, string> meaningByMemberId)
        {
            var meaningsByIli = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            using var reader = XmlReader.Create(path, CreateSettings());
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element
                    || !string.Equals(reader.LocalName, "Synset", StringComparison.Ordinal))
                {
                    continue;
                }

                var ili = Normalize(reader.GetAttribute("ili"));
                if (ili is null)
                {
                    continue;
                }

                if (!meaningsByIli.TryGetValue(ili, out var meanings))
                {
                    meanings = new List<string>();
                    meaningsByIli.Add(ili, meanings);
                }

                var memberIds = (reader.GetAttribute("members") ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                foreach (var memberId in memberIds)
                {
                    if (!meaningByMemberId.TryGetValue(memberId, out var meaning))
                    {
                        throw new LexiconBuildException(
                            $"Chinese Open WordNet 文件 {path} 的 synset {ili} 引用了不存在的 member：{memberId}。");
                    }

                    if (!meanings.Contains(meaning, StringComparer.Ordinal))
                    {
                        meanings.Add(meaning);
                    }
                }
            }

            return meaningsByIli.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.ToArray(),
                StringComparer.Ordinal);
        }

        private static XmlReaderSettings CreateSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().Normalize(NormalizationForm.FormC);
        }
    }
}
