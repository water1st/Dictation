using Edge_tts_sharp;
using Edge_tts_sharp.Model;
using System.Linq;
using System.Threading.Tasks;

namespace Dictation.Core
{
    [TTS(target: TTSTarget.Edge, "ja")]
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
            Task.Run(() =>
            {
                Edge_tts.PlayText(new PlayOption
                {
                    Rate = 0,
                    Volume = 1,
                    Text = word
                }, voice);
            });
        }
    }
}
