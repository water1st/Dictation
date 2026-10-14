using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 读取 WordNet YAML 目录并建立词元、义项和同义词集模型。
    /// </summary>
    internal sealed class WordNetYamlParser
    {
        private readonly IDeserializer deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// 解析指定目录的 WordNet YAML 文件并合并词元及同义词集。
        /// </summary>
        /// <param name="directoryPath">WordNet YAML 来源目录。</param>
        /// <returns>已解析的完整 WordNet 词元和同义词集。</returns>
        internal WordNetLexicon ParseDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("WordNet YAML 目录不能为空。", nameof(directoryPath));
            }

            var fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullDirectoryPath))
            {
                throw new DirectoryNotFoundException($"WordNet YAML 目录不存在：{fullDirectoryPath}。");
            }

            var files = WordNetSourceFiles.Enumerate(fullDirectoryPath);
            if (files.Count == 0)
            {
                throw new LexiconBuildException($"WordNet YAML 目录为空或没有受支持的文件：{fullDirectoryPath}。");
            }

            var lexemes = new List<ParsedLexeme>();
            var synsets = new List<ParsedSynset>();
            var lexemeIds = new HashSet<string>(StringComparer.Ordinal);
            var synsetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (file.Kind == WordNetSourceFileKind.Entries)
                {
                    ParseEntryFile(file, lexemes, lexemeIds);
                }
                else if (file.Kind == WordNetSourceFileKind.Synsets)
                {
                    ParseSynsetFile(file, synsets, synsetIds);
                }
            }

            if (lexemes.Count == 0 || synsets.Count == 0)
            {
                throw new LexiconBuildException(
                    $"WordNet YAML 缺少词条或 synset 文件：{fullDirectoryPath}。"
                    + $" lexemes={lexemes.Count}, synsets={synsets.Count}。");
            }

            ValidateReferences(lexemes, synsets);
            return new WordNetLexicon(
                lexemes
                    .Select(item => item.Lexeme)
                    .OrderBy(lexeme => lexeme.LexemeId, StringComparer.Ordinal)
                    .ToArray(),
                synsets
                    .Select(item => item.Synset)
                    .OrderBy(synset => synset.SynsetId, StringComparer.Ordinal)
                    .ToArray());
        }

        private void ParseEntryFile(
            WordNetSourceFile sourceFile,
            ICollection<ParsedLexeme> lexemes,
            ISet<string> lexemeIds)
        {
            var entries = Deserialize<Dictionary<string, Dictionary<string, RawLexicalEntry>>>(sourceFile);
            if (entries is null || entries.Count == 0)
            {
                throw CreateSourceException(sourceFile, "词条文件为空。");
            }

            foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var lemma = RequireSourceText(entry.Key, sourceFile, "lemma");
                if (entry.Value is null || entry.Value.Count == 0)
                {
                    throw CreateSourceException(sourceFile, $"词条 {lemma} 没有 lexical-entry。");
                }

                foreach (var lexicalEntry in entry.Value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var sourceEntryKey = RequireSourceText(lexicalEntry.Key, sourceFile, "lexical-entry key");
                    if (!TryParseSourceEntryKey(sourceEntryKey, out var partOfSpeech))
                    {
                        throw CreateSourceException(
                            sourceFile,
                            $"词条 {lemma} 的 lexical-entry key 无效：{sourceEntryKey}。只允许 n/v/a/r 及可选的 -正整数后缀。");
                    }

                    var rawLexicalEntry = lexicalEntry.Value
                        ?? throw CreateSourceException(sourceFile, $"词条 {lemma}/{sourceEntryKey} 的内容为空。");
                    if (rawLexicalEntry.Senses is null || rawLexicalEntry.Senses.Count == 0)
                    {
                        throw CreateSourceException(sourceFile, $"词条 {lemma}/{sourceEntryKey} 缺少 sense。");
                    }

                    var sourceForms = ParseSourceForms(rawLexicalEntry.Forms, sourceFile, lemma, sourceEntryKey);
                    var senses = new List<EnglishSense>(rawLexicalEntry.Senses.Count);
                    for (var index = 0; index < rawLexicalEntry.Senses.Count; index++)
                    {
                        var rawSense = rawLexicalEntry.Senses[index]
                            ?? throw CreateSourceException(sourceFile, $"词条 {lemma}/{sourceEntryKey} 的 sense {index + 1} 为空。");
                        var senseId = RequireSourceText(rawSense.Id, sourceFile, $"词条 {lemma}/{sourceEntryKey} 的 sense ID");
                        var synsetId = RequireSourceText(rawSense.Synset, sourceFile, $"sense {senseId} 的 synset");
                        var derivationIds = ParseDerivationIds(rawSense.Derivation, sourceFile, senseId);
                        senses.Add(new EnglishSense(senseId, synsetId, index + 1, derivationIds));
                    }

                    var lexemeId = $"{lemma}%{sourceEntryKey}";
                    if (!lexemeIds.Add(lexemeId))
                    {
                        throw CreateSourceException(sourceFile, $"重复 lexical-entry：{lexemeId}。");
                    }

                    lexemes.Add(new ParsedLexeme(
                        new EnglishLexeme(
                            lexemeId,
                            lemma,
                            sourceEntryKey,
                            partOfSpeech,
                            sourceForms,
                            senses,
                            lemma.Any(char.IsWhiteSpace)),
                        sourceFile.RelativePath));
                }
            }
        }

        private void ParseSynsetFile(
            WordNetSourceFile sourceFile,
            ICollection<ParsedSynset> synsets,
            ISet<string> synsetIds)
        {
            var rawSynsets = Deserialize<Dictionary<string, RawSynset>>(sourceFile);
            if (rawSynsets is null || rawSynsets.Count == 0)
            {
                throw CreateSourceException(sourceFile, "synset 文件为空。");
            }

            foreach (var pair in rawSynsets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var synsetId = RequireSourceText(pair.Key, sourceFile, "synset ID");
                var rawSynset = pair.Value
                    ?? throw CreateSourceException(sourceFile, $"synset {synsetId} 的内容为空。");
                var partOfSpeechCode = RequireSourceText(rawSynset.PartOfSpeech, sourceFile, $"synset {synsetId} 的 partOfSpeech");
                if (!TryParseSynsetPartOfSpeech(partOfSpeechCode, out var partOfSpeech))
                {
                    throw CreateSourceException(sourceFile, $"synset {synsetId} 的 partOfSpeech 无效：{partOfSpeechCode}。");
                }

                var definitions = ParseTexts(rawSynset.Definitions, sourceFile, $"synset {synsetId} 的 definition", required: true);
                var examples = ParseExamples(rawSynset.Examples, sourceFile, synsetId);
                if (!synsetIds.Add(synsetId))
                {
                    throw CreateSourceException(sourceFile, $"重复 synset：{synsetId}。");
                }

                synsets.Add(new ParsedSynset(
                    new EnglishSynset(synsetId, partOfSpeech, definitions, examples, rawSynset.Ili),
                    sourceFile.RelativePath));
            }
        }

        private T Deserialize<T>(WordNetSourceFile sourceFile)
        {
            try
            {
                using var stream = File.OpenRead(sourceFile.FullPath);
                using var reader = new StreamReader(stream);
                return deserializer.Deserialize<T>(reader)
                    ?? throw CreateSourceException(sourceFile, "YAML 根节点为空。");
            }
            catch (LexiconBuildException)
            {
                throw;
            }
            catch (Exception exception) when (exception is YamlException or IOException or InvalidOperationException)
            {
                throw new LexiconBuildException(
                    $"解析 WordNet YAML 失败：{sourceFile.RelativePath}。",
                    exception);
            }
        }

        private static IReadOnlyList<string> ParseSourceForms(
            IReadOnlyList<string>? values,
            WordNetSourceFile sourceFile,
            string lemma,
            string sourceEntryKey)
        {
            if (values is null)
            {
                return Array.Empty<string>();
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw CreateSourceException(sourceFile, $"词条 {lemma}/{sourceEntryKey} 含有空 form。");
                }

                result.Add(value);
            }

            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<string> ParseDerivationIds(
            IReadOnlyList<string>? values,
            WordNetSourceFile sourceFile,
            string senseId)
        {
            if (values is null)
            {
                return Array.Empty<string>();
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw CreateSourceException(sourceFile, $"sense {senseId} 含有空 derivation ID。");
                }

                result.Add(value);
            }

            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<string> ParseTexts(
            IReadOnlyList<string>? values,
            WordNetSourceFile sourceFile,
            string fieldName,
            bool required)
        {
            if (values is null)
            {
                if (required)
                {
                    throw CreateSourceException(sourceFile, $"{fieldName} 缺失。");
                }

                return Array.Empty<string>();
            }

            var result = new List<string>(values.Count);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw CreateSourceException(sourceFile, $"{fieldName} 含有空文本。");
                }

                result.Add(value);
            }

            if (required && result.Count == 0)
            {
                throw CreateSourceException(sourceFile, $"{fieldName} 不能为空。");
            }

            return result.ToArray();
        }

        private static IReadOnlyList<string> ParseExamples(
            IReadOnlyList<object>? values,
            WordNetSourceFile sourceFile,
            string synsetId)
        {
            if (values is null)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(values.Count);
            foreach (var value in values)
            {
                if (value is string text)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw CreateSourceException(sourceFile, $"synset {synsetId} 的 example 含有空文本。");
                    }

                    result.Add(text);
                    continue;
                }

                if (value is IDictionary mapping)
                {
                    string? mapText = null;
                    foreach (DictionaryEntry item in mapping)
                    {
                        if (string.Equals(item.Key?.ToString(), "text", StringComparison.Ordinal))
                        {
                            mapText = item.Value?.ToString();
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(mapText))
                    {
                        result.Add(mapText);
                        continue;
                    }
                }

                throw CreateSourceException(sourceFile, $"synset {synsetId} 的 example 格式不受支持。");
            }

            return result.ToArray();
        }

        /// <summary>
        /// 验证词元、义项、同义词集和派生引用的完整性，拒绝悬空来源关系。
        /// </summary>
        private static void ValidateReferences(
            IReadOnlyList<ParsedLexeme> lexemes,
            IReadOnlyList<ParsedSynset> synsets)
        {
            var synsetById = synsets.ToDictionary(item => item.Synset.SynsetId, StringComparer.Ordinal);
            var senses = new Dictionary<string, ParsedSense>(StringComparer.Ordinal);
            foreach (var item in lexemes)
            {
                foreach (var sense in item.Lexeme.Senses)
                {
                    var parsedSense = new ParsedSense(sense, item.SourceRelativePath);
                    if (!senses.TryAdd(sense.SenseId, parsedSense))
                    {
                        throw new LexiconBuildException(
                            $"WordNet 文件 {item.SourceRelativePath}：sense ID 重复：{sense.SenseId}。"
                            + $" 已在 {senses[sense.SenseId].SourceRelativePath} 出现。");
                    }

                    if (!synsetById.TryGetValue(sense.SynsetId, out var parsedSynset))
                    {
                        throw new LexiconBuildException(
                            $"WordNet 文件 {item.SourceRelativePath}：sense {sense.SenseId} 引用了不存在的 synset：{sense.SynsetId}。");
                    }

                    if (!IsCompatiblePartOfSpeech(item.Lexeme.PartOfSpeech, parsedSynset.Synset.PartOfSpeech))
                    {
                        throw new LexiconBuildException(
                            $"WordNet 文件 {item.SourceRelativePath}：sense {sense.SenseId} 的词性与 synset {sense.SynsetId} 不一致。"
                            + $" lexeme={ToCode(item.Lexeme.PartOfSpeech)}, synset={ToCode(parsedSynset.Synset.PartOfSpeech)}。");
                    }
                }
            }

            foreach (var item in lexemes)
            {
                foreach (var sense in item.Lexeme.Senses)
                {
                    foreach (var derivationId in sense.DerivationSenseIds)
                    {
                        if (!senses.ContainsKey(derivationId))
                        {
                            throw new LexiconBuildException(
                                $"WordNet 文件 {item.SourceRelativePath}：sense {sense.SenseId} 引用了不存在的 derivation sense：{derivationId}。");
                        }
                    }
                }
            }
        }

        private static bool TryParseSourceEntryKey(string value, out EnglishPartOfSpeech partOfSpeech)
        {
            partOfSpeech = value switch
            {
                "n" => EnglishPartOfSpeech.Noun,
                "v" => EnglishPartOfSpeech.Verb,
                "a" => EnglishPartOfSpeech.Adjective,
                "r" => EnglishPartOfSpeech.Adverb,
                _ => EnglishPartOfSpeech.Unknown
            };
            if (partOfSpeech != EnglishPartOfSpeech.Unknown)
            {
                return true;
            }

            var separatorIndex = value.IndexOf('-');
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                return false;
            }

            var baseCode = value[..separatorIndex];
            if (baseCode is not "n" and not "v" and not "a" and not "r")
            {
                return false;
            }

            var suffix = value[(separatorIndex + 1)..];
            if (suffix.Any(character => character is < '0' or > '9')
                || !int.TryParse(suffix, out var number)
                || number <= 0)
            {
                return false;
            }

            partOfSpeech = baseCode switch
            {
                "n" => EnglishPartOfSpeech.Noun,
                "v" => EnglishPartOfSpeech.Verb,
                "a" => EnglishPartOfSpeech.Adjective,
                "r" => EnglishPartOfSpeech.Adverb,
                _ => EnglishPartOfSpeech.Unknown
            };
            return true;
        }

        private static bool TryParseSynsetPartOfSpeech(string value, out EnglishPartOfSpeech partOfSpeech)
        {
            partOfSpeech = value switch
            {
                "n" => EnglishPartOfSpeech.Noun,
                "v" => EnglishPartOfSpeech.Verb,
                "a" => EnglishPartOfSpeech.Adjective,
                "s" => EnglishPartOfSpeech.AdjectiveSatellite,
                "r" => EnglishPartOfSpeech.Adverb,
                _ => EnglishPartOfSpeech.Unknown
            };
            return partOfSpeech != EnglishPartOfSpeech.Unknown;
        }

        private static bool IsCompatiblePartOfSpeech(
            EnglishPartOfSpeech lexemePartOfSpeech,
            EnglishPartOfSpeech synsetPartOfSpeech)
        {
            return lexemePartOfSpeech == synsetPartOfSpeech
                || (lexemePartOfSpeech == EnglishPartOfSpeech.Adjective
                    && synsetPartOfSpeech == EnglishPartOfSpeech.AdjectiveSatellite);
        }

        private static string ToCode(EnglishPartOfSpeech partOfSpeech)
        {
            return partOfSpeech switch
            {
                EnglishPartOfSpeech.Noun => "n",
                EnglishPartOfSpeech.Verb => "v",
                EnglishPartOfSpeech.Adjective => "a",
                EnglishPartOfSpeech.AdjectiveSatellite => "s",
                EnglishPartOfSpeech.Adverb => "r",
                _ => "unknown"
            };
        }

        private static string RequireSourceText(string? value, WordNetSourceFile sourceFile, string fieldName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw CreateSourceException(sourceFile, $"{fieldName} 缺失或为空。")
                : value;
        }

        private static LexiconBuildException CreateSourceException(WordNetSourceFile sourceFile, string message)
        {
            return new LexiconBuildException($"WordNet 文件 {sourceFile.RelativePath}：{message}");
        }

        private readonly record struct ParsedLexeme(EnglishLexeme Lexeme, string SourceRelativePath);

        private readonly record struct ParsedSynset(EnglishSynset Synset, string SourceRelativePath);

        private readonly record struct ParsedSense(EnglishSense Sense, string SourceRelativePath);

        private sealed class RawLexicalEntry
        {
            [YamlMember(Alias = "form")]
            public List<string>? Forms { get; set; }

            [YamlMember(Alias = "sense")]
            public List<RawSense>? Senses { get; set; }
        }

        private sealed class RawSense
        {
            [YamlMember(Alias = "derivation")]
            public List<string>? Derivation { get; set; }

            [YamlMember(Alias = "id")]
            public string? Id { get; set; }

            [YamlMember(Alias = "synset")]
            public string? Synset { get; set; }
        }

        private sealed class RawSynset
        {
            [YamlMember(Alias = "definition")]
            public List<string>? Definitions { get; set; }

            [YamlMember(Alias = "example")]
            public List<object>? Examples { get; set; }

            [YamlMember(Alias = "ili")]
            public string? Ili { get; set; }

            [YamlMember(Alias = "partOfSpeech")]
            public string? PartOfSpeech { get; set; }
        }
    }
}
