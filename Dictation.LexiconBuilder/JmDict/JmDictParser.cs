using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.JmDict
{
    internal sealed class JmDictParser
    {
        private static readonly Regex CreatedDatePattern = new(
            @"JMdict\s+created:\s*(?<date>\d{4}-\d{2}-\d{2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        internal string CreatedDate { get; private set; } = "unknown";

        internal IEnumerable<JmDictEntry> Parse(Stream xmlStream)
        {
            ArgumentNullException.ThrowIfNull(xmlStream);
            if (!xmlStream.CanRead)
            {
                throw new ArgumentException("JMdict 输入流必须可读。", nameof(xmlStream));
            }

            return ParseCore(xmlStream);
        }

        private IEnumerable<JmDictEntry> ParseCore(Stream xmlStream)
        {
            CreatedDate = "unknown";
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null,
                CloseInput = false,
                IgnoreComments = false,
                IgnoreWhitespace = true,
                MaxCharactersFromEntities = 32L * 1024 * 1024
            };

            using var reader = XmlReader.Create(xmlStream, settings);
            var sawNode = false;
            var readNext = true;
            while (true)
            {
                XElement? entryElement = null;
                var emptyDocument = false;
                var entryLineNumber = 0;
                var entryLinePosition = 0;
                try
                {
                    if (readNext && !reader.Read())
                    {
                        yield break;
                    }

                    readNext = true;
                    sawNode = true;

                    if (reader.NodeType == XmlNodeType.Comment)
                    {
                        var match = CreatedDatePattern.Match(reader.Value);
                        if (match.Success)
                        {
                            CreatedDate = match.Groups["date"].Value;
                        }

                        continue;
                    }

                    if (reader.NodeType != XmlNodeType.Element
                        || !string.Equals(reader.LocalName, "entry", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (reader is IXmlLineInfo entryLineInfo && entryLineInfo.HasLineInfo())
                    {
                        entryLineNumber = entryLineInfo.LineNumber;
                        entryLinePosition = entryLineInfo.LinePosition;
                    }

                    entryElement = (XElement)XNode.ReadFrom(reader);
                    readNext = false;
                }
                catch (XmlException exception) when (!sawNode
                    && exception.Message.Contains("Root element is missing", StringComparison.OrdinalIgnoreCase))
                {
                    emptyDocument = true;
                }
                catch (Exception exception) when (exception is XmlException or IOException or InvalidOperationException)
                {
                    throw CreateParseException(
                        reader,
                        "解析 JMdict 失败。",
                        exception,
                        entryLineNumber,
                        entryLinePosition);
                }

                if (emptyDocument)
                {
                    yield break;
                }

                if (entryElement is not null)
                {
                    JmDictEntry parsedEntry;
                    try
                    {
                        parsedEntry = ParseEntry(entryElement);
                    }
                    catch (LexiconBuildException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new LexiconBuildException("构造 JMdict entry 失败。", exception);
                    }

                    yield return parsedEntry;
                }
            }
        }

        private static JmDictEntry ParseEntry(XElement entryElement)
        {
            var sequenceText = ChildValue(entryElement, "ent_seq");
            if (!int.TryParse(sequenceText, out var sequence) || sequence <= 0)
            {
                throw new LexiconBuildException($"JMdict entry 的 ent_seq 无效：{sequenceText ?? "(缺失)"}。");
            }

            var orthographies = entryElement
                .Elements()
                .Where(x => NameIs(x, "k_ele"))
                .Select(ParseOrthography)
                .ToArray();
            var readings = entryElement
                .Elements()
                .Where(x => NameIs(x, "r_ele"))
                .Select(ParseReading)
                .ToArray();
            if (readings.Length == 0)
            {
                throw new LexiconBuildException($"JMdict entry {sequence} 缺少必需 reading。");
            }

            var senseElements = entryElement
                .Elements()
                .Where(x => NameIs(x, "sense"))
                .ToArray();
            if (senseElements.Length == 0)
            {
                throw new LexiconBuildException($"JMdict entry {sequence} 缺少 sense。");
            }

            var senses = new List<JmDictSense>();
            IReadOnlyList<string> inheritedPartsOfSpeech = Array.Empty<string>();
            for (var index = 0; index < senseElements.Length; index++)
            {
                var senseElement = senseElements[index];
                var sensePartsOfSpeech = ChildValues(senseElement, "pos");
                if (sensePartsOfSpeech.Count == 0)
                {
                    sensePartsOfSpeech = inheritedPartsOfSpeech;
                }
                else
                {
                    inheritedPartsOfSpeech = sensePartsOfSpeech;
                }

                var glosses = senseElement
                    .Elements()
                    .Where(x => NameIs(x, "gloss"))
                    .Select(ParseGloss)
                    .Where(gloss => gloss is not null)
                    .Cast<JmDictGloss>()
                    .ToArray();
                var hasEnglishGloss = glosses.Any(gloss =>
                    string.Equals(gloss.Language, "eng", StringComparison.OrdinalIgnoreCase));
                if (hasEnglishGloss)
                {
                    senses.Add(new JmDictSense(
                        index + 1,
                        sensePartsOfSpeech,
                        ChildValues(senseElement, "stagk"),
                        ChildValues(senseElement, "stagr"),
                        glosses));
                }
            }

            return new JmDictEntry(sequence, orthographies, readings, senses);
        }

        private static JmDictOrthography ParseOrthography(XElement element)
        {
            var text = ChildValue(element, "keb");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new LexiconBuildException("JMdict k_ele 缺少 keb。");
            }

            return new JmDictOrthography(text, ChildValues(element, "ke_pri"));
        }

        private static JmDictReading ParseReading(XElement element)
        {
            var text = ChildValue(element, "reb");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new LexiconBuildException("JMdict r_ele 缺少 reb。");
            }

            return new JmDictReading(
                text,
                ChildValues(element, "re_pri"),
                ChildValues(element, "re_restr"),
                element.Elements().Any(x => NameIs(x, "re_nokanji")));
        }

        private static JmDictGloss? ParseGloss(XElement element)
        {
            var text = element.Value.Trim();
            if (text.Length == 0)
            {
                return null;
            }

            var language = element.Attributes().FirstOrDefault(attribute =>
                attribute.Name == XNamespace.Xml + "lang")?.Value;
            var type = element.Attribute("g_type")?.Value;
            return new JmDictGloss(language ?? "eng", text, type);
        }

        private static IReadOnlyList<string> ChildValues(XElement element, string name)
        {
            return element
                .Elements()
                .Where(child => NameIs(child, name))
                .Select(child => child.Value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
        }

        private static string? ChildValue(XElement element, string name)
        {
            return element
                .Elements()
                .FirstOrDefault(child => NameIs(child, name))?.Value.Trim();
        }

        private static bool NameIs(XElement element, string name)
        {
            return string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);
        }

        private static LexiconBuildException CreateParseException(
            XmlReader reader,
            string message,
            Exception exception,
            int fallbackLineNumber,
            int fallbackLinePosition)
        {
            var lineNumber = 0;
            var linePosition = 0;
            if (exception is XmlException xmlException
                && (xmlException.LineNumber > 0 || xmlException.LinePosition > 0))
            {
                lineNumber = xmlException.LineNumber;
                linePosition = xmlException.LinePosition;
            }

            if (reader is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                if (lineNumber == 0)
                {
                    lineNumber = lineInfo.LineNumber;
                }

                if (linePosition == 0)
                {
                    linePosition = lineInfo.LinePosition;
                }
            }

            if (lineNumber == 0)
            {
                lineNumber = fallbackLineNumber;
            }

            if (linePosition == 0)
            {
                linePosition = fallbackLinePosition;
            }

            return new LexiconBuildException(
                $"{message} XML 位置 {lineNumber}:{linePosition}。",
                exception);
        }
    }
}
