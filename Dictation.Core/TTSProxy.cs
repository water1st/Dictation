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

        public void Play(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("单词不能为空或仅为空格。", nameof(word));

            var speakWord = word;
            if (word.Contains(SEPARATOR))
            {
                speakWord = word.Split(SEPARATOR)[0].Trim();
            }

            player.Play(speakWord);
        }
    }
}
