using System.Text.RegularExpressions;

namespace Dictation.Core
{
    internal class LanguageRecognizer
    {
        public string Recognize(string word)
        {
            // 如果包含平假名、片假名或日语汉字
            if (Regex.IsMatch(word, @"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\u3400-\u4DBF\u20000-\u2A6DF\u2A700-\u2B73F\u2B740-\u2B81F\u2B820-\u2CEA1]"))
            {
                return "ja";  // 日语
            }

            // 如果是全汉字，且不包含日语字符
            if (Regex.IsMatch(word, @"^[\u4E00-\u9FA5]+$"))
            {
                return "zh";  // 中文
            }

            // 如果是全英文
            if (Regex.IsMatch(word, @"^[a-zA-Z]+$"))
            {
                return "en";  // 英文
            }

            // 默认情况下返回英文
            return "en";  // 默认为英文
        }
    }
}
