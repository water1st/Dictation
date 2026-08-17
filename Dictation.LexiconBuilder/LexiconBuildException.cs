using System;

namespace Dictation.LexiconBuilder
{
    internal sealed class LexiconBuildException : Exception
    {
        public LexiconBuildException(string message)
            : base(message)
        {
        }

        public LexiconBuildException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
