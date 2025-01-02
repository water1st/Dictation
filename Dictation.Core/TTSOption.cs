using System;
using System.Collections.Generic;

namespace Dictation.Core
{
    public class TTSOption
    {
        private static readonly Lazy<TTSOption> instance = new Lazy<TTSOption>(() => new TTSOption());
        public readonly static Dictionary<string, string> SupportLanguages = new Dictionary<string, string>
        {
            { "edge-jp","日语（edge）" },
            {"ja","日语"},
            {"en","英语"}

        };

        private TTSOption() { }

        public static TTSOption Instance => instance.Value;

        public KeyValuePair<string, string> Language { get; set; }

    }
}
