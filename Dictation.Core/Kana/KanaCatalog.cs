namespace Dictation.Core.Kana
{
    /// <summary>提供内置现代假名事实表，不依赖运行词库或数据库。</summary>
    internal static class KanaCatalog
    {
        internal static IReadOnlyList<KanaSymbol> Items { get; } = Array.AsReadOnly(CreateItems());

        /// <summary>按启用字形与音类选择固定事实，不选择未来记忆或计算每日额度。</summary>
        /// <param name="plan">已校验五十音计划。</param>
        /// <returns>每个独立字形只出现一次的范围。</returns>
        internal static IReadOnlyList<KanaSymbol> Select(KanaPlanDto plan) => Items.Where(item =>
            (item.Script == KanaScript.Hiragana ? plan.Hiragana : plan.Katakana)
            && (item.Range switch { KanaRange.Clear => plan.Clear, KanaRange.Voiced => plan.Voiced, _ => plan.Contracted })).ToArray();

        /// <summary>构建现代基本清音、浊音／半浊音及拗音的两种字形。</summary>
        /// <returns>不含古假名或外来音扩展的214个项目。</returns>
        private static KanaSymbol[] CreateItems()
        {
            var groups = new[]
            {
                (KanaRange.Clear, "あ い う え お か き く け こ さ し す せ そ た ち つ て と な に ぬ ね の は ひ ふ へ ほ ま み む め も や ゆ よ ら り る れ ろ わ を ん"),
                (KanaRange.Voiced, "が ぎ ぐ げ ご ざ じ ず ぜ ぞ だ ぢ づ で ど ば び ぶ べ ぼ ぱ ぴ ぷ ぺ ぽ"),
                (KanaRange.Contracted, "きゃ きゅ きょ ぎゃ ぎゅ ぎょ しゃ しゅ しょ じゃ じゅ じょ ちゃ ちゅ ちょ ぢゃ ぢゅ ぢょ にゃ にゅ にょ ひゃ ひゅ ひょ びゃ びゅ びょ ぴゃ ぴゅ ぴょ みゃ みゅ みょ りゃ りゅ りょ")
            };
            var result = new List<KanaSymbol>();
            foreach (var (range, text) in groups)
            foreach (var hiragana in text.Split(' '))
            {
                // 基本表内片假名与平假名具有固定Unicode偏移，不接受任意文本转换。
                var katakana = new string(hiragana.Select(character => (char)(character + 0x60)).ToArray());
                var reading = hiragana == "を" ? "お" : hiragana;
                var hint = hiragana switch
                {
                    "お" => "あ行", "を" => "わ行", "じ" or "ず" => "さ行", "ぢ" or "づ" => "た行",
                    "じゃ" or "じゅ" or "じょ" => "ざ行", "ぢゃ" or "ぢゅ" or "ぢょ" => "だ行", _ => ""
                };
                result.Add(new($"Hiragana:{hiragana}", hiragana, reading, KanaScript.Hiragana, range, hint));
                result.Add(new($"Katakana:{katakana}", katakana, reading, KanaScript.Katakana, range, hint));
            }
            return result.ToArray();
        }
    }
}
