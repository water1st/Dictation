using System;

namespace Dictation.Core
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TTSAttribute : Attribute
    {
        public TTSAttribute(TTSTarget target, string language)
        {
            Target = target;
            Language = language;
        }

        public string Language { get; }
        public TTSTarget Target { get; }
    }
}
