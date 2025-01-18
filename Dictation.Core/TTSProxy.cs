using System;

namespace Dictation.Core
{
    internal class TTSProxy : ITTSPlayer
    {
        private const char SEPARATOR = '_';
        private readonly ITTSPlayer player;

        public TTSProxy(ITTSPlayer player)
        {
            this.player = player;
        }

        public void Dispose()
        {
            player.Dispose();
        }

        public void Play(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("单词不能为空或仅为空格。", nameof(word));

            var speakWord = word;
            if (word.Contains(SEPARATOR))
            {
                var wordSplits = word.Split(SEPARATOR);
                if (TTSOption.Instance.PlayMod == "play_meaning")
                {
                    speakWord = wordSplits[0];
                }
                else if (TTSOption.Instance.PlayMod == "play_pronunciation")
                {
                    if (wordSplits.Length > 1)
                        speakWord = wordSplits[1];
                }
            }
            speakWord = speakWord.Trim();

            player.Play(speakWord);
        }
    }
}
