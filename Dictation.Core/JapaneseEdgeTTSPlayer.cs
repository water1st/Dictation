using Edge_tts_sharp;
using Edge_tts_sharp.Model;
using System.Linq;

namespace Dictation.Core
{
    internal class JapaneseEdgeTTSPlayer : ITTSPlayer
    {
        private readonly eVoice voice;
        public JapaneseEdgeTTSPlayer()
        {
            var voices = Edge_tts.GetVoice();
            voice = voices.FirstOrDefault(c => c.ShortName == "ja-JP-NanamiNeural");
        }

        public void Play(string word)
        {
            var speakWord = word;
            if (word.Contains(','))
            {
                speakWord = word.Split(',')[0].Trim();
            }

            Edge_tts.PlayText(new PlayOption
            {
                Rate = 0,
                Volume = 1,
                Text = speakWord
            }, voice);
        }
    }
}
