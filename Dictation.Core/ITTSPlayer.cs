using System;

namespace Dictation.Core
{
    public interface ITTSPlayer : IDisposable
    {
        void Play(string word);
    }
}
