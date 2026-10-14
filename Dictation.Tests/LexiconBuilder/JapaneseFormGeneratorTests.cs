using System;
using System.Collections.Generic;
using System.Linq;

using Dictation.LexiconBuilder.JmDict;

namespace Dictation.Tests.LexiconBuilder
{

    /// <summary>
    /// 验证日语词形生成、活用、词汇角色及书写限制。
    /// </summary>
    public sealed class JapaneseFormGeneratorTests
    {
        [Theory]
        [InlineData("食べる", "たべる", "食べます", "食べて", "食べた")]
        [InlineData("書く", "かく", "書きます", "書いて", "書いた")]
        [InlineData("泳ぐ", "およぐ", "泳ぎます", "泳いで", "泳いだ")]
        [InlineData("行く", "いく", "行きます", "行って", "行った")]
        [InlineData("掃除する", "そうじする", "掃除します", "掃除して", "掃除した")]
        [InlineData("来る", "くる", "来ます", "来て", "来た")]
        [InlineData("問う", "とう", "問います", "問うて", "問うた")]
        public void Generate_ProducesExpectedKanjiForms(
            string dictionary,
            string reading,
            string polite,
            string te,
            string ta)
        {
            var entry = CreateVerbEntry(dictionary, reading);
            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Equal(dictionary, Find(forms, JapaneseFormKind.Dictionary, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(polite, Find(forms, JapaneseFormKind.Polite, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(te, Find(forms, JapaneseFormKind.Te, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(ta, Find(forms, JapaneseFormKind.Ta, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(reading, Find(forms, JapaneseFormKind.Dictionary, JapaneseScriptKind.Kana).Reading);
        }

        [Theory]
        [InlineData("話す", "はなす", "話します", "話して", "話した")]
        [InlineData("待つ", "まつ", "待ちます", "待って", "待った")]
        [InlineData("死ぬ", "しぬ", "死にます", "死んで", "死んだ")]
        [InlineData("遊ぶ", "あそぶ", "遊びます", "遊んで", "遊んだ")]
        [InlineData("読む", "よむ", "読みます", "読んで", "読んだ")]
        [InlineData("買う", "かう", "買います", "買って", "買った")]
        [InlineData("ある", "ある", "あります", "あって", "あった")]
        public void Generate_CoversModernGodanClasses(
            string dictionary,
            string reading,
            string polite,
            string te,
            string ta)
        {
            var entry = CreateVerbEntry(dictionary, reading);
            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Equal(polite, Find(forms, JapaneseFormKind.Polite, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(te, Find(forms, JapaneseFormKind.Te, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal(ta, Find(forms, JapaneseFormKind.Ta, JapaneseScriptKind.Kanji).Surface);
        }

        [Fact]
        public void Generate_UsesOnlyDictionaryForNounPhraseAndUnknownVerb()
        {
            var generator = new JapaneseFormGenerator();

            var noun = CreateEntry("掃除", "そうじ", "Common noun");
            var phrase = CreateEntry("気を付ける", "きをつける", "Expressions (phrases, clauses, etc.)");
            var unknown = CreateEntry("古語", "こご", "Godan verb with 'x' ending");

            Assert.All(generator.Generate(noun, noun.Senses[0]), form => Assert.Equal(JapaneseFormKind.Dictionary, form.FormKind));
            Assert.All(generator.Generate(phrase, phrase.Senses[0]), form => Assert.Equal(JapaneseFormKind.Dictionary, form.FormKind));
            Assert.All(generator.Generate(unknown, unknown.Senses[0]), form => Assert.Equal(JapaneseFormKind.Dictionary, form.FormKind));
        }

        [Fact]
        public void Generate_DoesNotTreatAdjectivalNounAsNounRole()
        {
            var entry = CreateEntry("静か", "しずか", "adjectival noun (keiyodoshi)");
            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.NotEmpty(forms);
            Assert.All(forms, form => Assert.Equal(JapaneseLexicalRole.Adjective, form.LexicalRole));
            Assert.DoesNotContain(forms, form => form.LexicalRole == JapaneseLexicalRole.Noun);
        }

        [Fact]
        public void Generate_RespectsReadingAndSenseRestrictionsAndNoKanji()
        {
            var entry = new JmDictEntry(
                1,
                new[]
                {
                    new JmDictOrthography("書く", new[] { "ichi1" }),
                    new JmDictOrthography("描く", Array.Empty<string>())
                },
                new[]
                {
                    new JmDictReading("かく", Array.Empty<string>(), new[] { "書く" }, noKanji: false),
                    new JmDictReading("カク", Array.Empty<string>(), Array.Empty<string>(), noKanji: true)
                },
                new[]
                {
                    new JmDictSense(1, new[] { "Godan verb with 'ku' ending" }, new[] { "書く" }, new[] { "かく" }, new[] { new JmDictGloss("eng", "to write") })
                });

            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Contains(forms, form => form.Surface == "書いて" && form.ScriptKind == JapaneseScriptKind.Kanji);
            Assert.DoesNotContain(forms, form => form.Surface == "描いて");
            Assert.DoesNotContain(forms, form => form.ScriptKind == JapaneseScriptKind.Kana && form.Reading == "カク");
            Assert.DoesNotContain(forms, form => form.ScriptKind == JapaneseScriptKind.Kanji && form.Reading == "カク");
        }

        [Fact]
        public void Generate_UsesZuruRuleBeforeIchidanRule()
        {
            var entry = CreateEntry("論ずる", "ろんずる", "Ichidan verb - zuru verb (alternative form of -jiru verbs)");
            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Equal("論じます", Find(forms, JapaneseFormKind.Polite, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal("論じて", Find(forms, JapaneseFormKind.Te, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal("論じた", Find(forms, JapaneseFormKind.Ta, JapaneseScriptKind.Kanji).Surface);
        }

        [Fact]
        public void Generate_UsesRealSuruNounPosAsTwoLexicalRoles()
        {
            var entry = new JmDictEntry(
                1399790,
                new[] { new JmDictOrthography("掃除", new[] { "ichi1" }) },
                new[] { new JmDictReading("そうじ", new[] { "ichi1" }, Array.Empty<string>(), noKanji: false) },
                new[]
                {
                    new JmDictSense(
                        1,
                        new[]
                        {
                            "noun (common) (futsuumeishi)",
                            "noun or participle which takes the aux. verb suru",
                            "transitive verb"
                        },
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        new[] { new JmDictGloss("eng", "cleaning") })
                });

            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Contains(forms, form => form.Surface == "掃除" && form.FormKind == JapaneseFormKind.Dictionary && form.LexicalRole == JapaneseLexicalRole.Noun);
            Assert.Contains(forms, form => form.Surface == "掃除する" && form.FormKind == JapaneseFormKind.Dictionary && form.LexicalRole == JapaneseLexicalRole.Verb);
            Assert.Contains(forms, form => form.Surface == "掃除します" && form.FormKind == JapaneseFormKind.Polite && form.LexicalRole == JapaneseLexicalRole.Verb);
            Assert.Contains(forms, form => form.Surface == "掃除して" && form.FormKind == JapaneseFormKind.Te && form.LexicalRole == JapaneseLexicalRole.Verb);
            Assert.Contains(forms, form => form.Surface == "掃除した" && form.FormKind == JapaneseFormKind.Ta && form.LexicalRole == JapaneseLexicalRole.Verb);
            Assert.Single(forms, form => form.Surface == "掃除" && form.FormKind == JapaneseFormKind.Dictionary);
        }

        [Fact]
        public void Generate_UsesRealAruSpecialClassAndSkipsOldUruClass()
        {
            var aru = CreateEntry("くださる", "くださる", "Godan verb - -aru special class");
            var aruForms = new JapaneseFormGenerator().Generate(aru, aru.Senses[0]);
            Assert.Equal("くださいます", Find(aruForms, JapaneseFormKind.Polite, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal("くださって", Find(aruForms, JapaneseFormKind.Te, JapaneseScriptKind.Kanji).Surface);
            Assert.Equal("くださった", Find(aruForms, JapaneseFormKind.Ta, JapaneseScriptKind.Kanji).Surface);

            var oldUru = CreateEntry("得る", "える", "Godan verb - Uru old class verb (old form of Eru)");
            var oldUruForms = new JapaneseFormGenerator().Generate(oldUru, oldUru.Senses[0]);
            Assert.NotEmpty(oldUruForms);
            Assert.All(oldUruForms, form => Assert.Equal(JapaneseFormKind.Dictionary, form.FormKind));
        }

        [Fact]
        public void Generate_SkipsInflectedFormsWhenSurfaceDoesNotMatchItsVerbClass()
        {
            var entry = CreateEntry("古語", "こご", "Godan verb with 'ku' ending");
            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.NotEmpty(forms);
            Assert.All(forms, form => Assert.Equal(JapaneseFormKind.Dictionary, form.FormKind));
        }

        [Fact]
        public void Generate_DeduplicatesFormsAndKeepsStableOrder()
        {
            var entry = new JmDictEntry(
                1,
                new[] { new JmDictOrthography("食べる", new[] { "ichi1" }) },
                new[]
                {
                    new JmDictReading("たべる", new[] { "ichi1" }, Array.Empty<string>(), noKanji: false),
                    new JmDictReading("たべる", Array.Empty<string>(), Array.Empty<string>(), noKanji: false)
                },
                new[]
                {
                    new JmDictSense(1, new[] { "Ichidan verb" }, Array.Empty<string>(), Array.Empty<string>(), new[] { new JmDictGloss("eng", "to eat") })
                });

            var forms = new JapaneseFormGenerator().Generate(entry, entry.Senses[0]);

            Assert.Equal(forms.Count, forms.DistinctBy(form => $"{form.FormKind}|{form.ScriptKind}|{form.Surface}|{form.Reading}").Count());
            Assert.Equal(new[] { JapaneseFormKind.Dictionary, JapaneseFormKind.Dictionary, JapaneseFormKind.Polite }, forms.Take(3).Select(form => form.FormKind));
            Assert.True(forms[0].IsPrimary);
        }

        private static JmDictEntry CreateVerbEntry(string dictionary, string reading)
        {
            var pos = dictionary switch
            {
                "食べる" => "Ichidan verb",
                "書く" => "Godan verb with 'ku' ending",
                "泳ぐ" => "Godan verb with 'gu' ending",
                "行く" => "Godan verb - iku/yuku special class",
                "掃除する" => "Suru verb - irregular",
                "来る" => "Kuru verb - special class",
                "問う" => "Godan verb with 'u' ending (special class)",
                "話す" => "Godan verb with 'su' ending",
                "待つ" => "Godan verb with 'tsu' ending",
                "死ぬ" => "Godan verb with 'nu' ending",
                "遊ぶ" => "Godan verb with 'bu' ending",
                "読む" => "Godan verb with 'mu' ending",
                "買う" => "Godan verb with 'u' ending",
                "ある" => "Godan verb - -aru special class",
                _ => "Godan verb with 'x' ending"
            };
            return CreateEntry(dictionary, reading, pos);
        }

        private static JmDictEntry CreateEntry(string dictionary, string reading, string pos)
        {
            return new JmDictEntry(
                1,
                new[] { new JmDictOrthography(dictionary, new[] { "ichi1" }) },
                new[] { new JmDictReading(reading, new[] { "ichi1" }, Array.Empty<string>(), noKanji: false) },
                new[] { new JmDictSense(1, new[] { pos }, Array.Empty<string>(), Array.Empty<string>(), new[] { new JmDictGloss("eng", "meaning") }) });
        }

        private static JapaneseLexiconForm Find(
            IReadOnlyList<JapaneseLexiconForm> forms,
            JapaneseFormKind formKind,
            JapaneseScriptKind scriptKind,
            JapaneseLexicalRole lexicalRole = JapaneseLexicalRole.Verb)
        {
            return forms.Single(form => form.FormKind == formKind
                && form.ScriptKind == scriptKind
                && form.LexicalRole == lexicalRole);
        }
    }
}
