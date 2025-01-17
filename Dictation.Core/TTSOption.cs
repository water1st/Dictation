﻿using System;
using System.Collections.Generic;

namespace Dictation.Core
{
    public class TTSOption
    {
        private static readonly Lazy<TTSOption> instance = new Lazy<TTSOption>(() => new TTSOption());
        public readonly static Dictionary<string, string> SupportLanguages = new Dictionary<string, string>
        {
            { "edge_zh","汉语（edge）" },
            { "edge_ja","日语（edge）" },
            { "edge_en","英语（edge）" },
            
            { "system_ja","日语"},
            { "system_en","英语"},
            { "system_zh","汉语"}

        };

        private TTSOption() { }

        public static TTSOption Instance => instance.Value;

        public KeyValuePair<string, string> Language { get; set; }

    }
}
