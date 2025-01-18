using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dictation.Core
{
    public class TTSOption
    {
        private static readonly Lazy<TTSOption> instance = new Lazy<TTSOption>(() => new TTSOption());
        public readonly static ReadOnlyDictionary<string, string> SupportLanguages = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>
        {
            { "edge_zh","汉语（edge）" },
            { "edge_ja","日语（edge）" },
            { "edge_en","英语（edge）" },

            { "system_ja","日语"},
            { "system_en","英语"},
            { "system_zh","汉语"}

        });

        public readonly static ReadOnlyDictionary<string, string> SupportPlayMods = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string> {
                {"play_meaning","读意" },
                {"play_pronunciation","读音"}
            });


        private KeyValuePair<string, string> language;

        private TTSOption() { }

        public static TTSOption Instance => instance.Value;

        public string Target { get; private set; }
        public string LanguageName { get; private set; }

        public string PlayMod { get; set; }

        public KeyValuePair<string, string> Language
        {
            get
            {
                return language;
            }
            set
            {
                language = value;
                var args = language.Key.Split('_');
                Target = args[0];
                LanguageName = args[1];
            }
        }

    }
}
