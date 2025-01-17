using System;

namespace Dictation.Core
{
    public class TTSFactory
    {
        public ITTSPlayer CreateTTSPlayer(string language)
        {
            ITTSPlayer tts;
            if (TTSOption.Instance.Target == "system")
                tts = new SystemTTSPlayer(language);
            else if (TTSOption.Instance.Target == "edge")
                tts = new EdgeTTSPlayer(language);
            else
                throw new NotSupportedException($"不支持{TTSOption.Instance.Target} TTS");

            return new TTSProxy(tts);
        }
    }
}
