using Edge_tts_sharp;
using System.Linq;

namespace Dictation.Core
{
    internal class JapaneseEdgeTTSPlayer : ITTSPlayer
    {
        public void Play(string word)
        {
            var voices = Edge_tts.GetVoice();
            var voice = voices.FirstOrDefault(c => c.ShortName == "ja-JP-NanamiNeural");

            var speakWord = word;
            if (word.Contains(','))
            {
                speakWord = word.Split(',')[0].Trim();
            }

            Edge_tts.PlayText(new Edge_tts_sharp.Model.PlayOption
            {
                Rate = 0,
                Volume = 1,
                Text = speakWord
            }, voice);
        }
    }
}
