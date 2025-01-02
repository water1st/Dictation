
using System;
using System.Linq;
using System.Speech.Synthesis;

namespace Dictation.Core
{
    internal class EnglishTTSPlayer : ITTSPlayer, IDisposable
    {
        private SpeechSynthesizer synthesizer;

        public EnglishTTSPlayer()
        {
            synthesizer = new SpeechSynthesizer();

            // 设置语速和音量
            synthesizer.Rate = 0; // 语速：-10 (最慢) 到 10 (最快)
            synthesizer.Volume = 100; // 音量：0 到 100

            // 设置日语语音
            SetJapaneseVoice();
        }

        private void SetJapaneseVoice()
        {
            try
            {
                var voices = synthesizer.GetInstalledVoices();

                var voice = voices.FirstOrDefault(v => v?.VoiceInfo?.Culture?.TwoLetterISOLanguageName == TTSOption.Instance.Language.Key);

                if (voice != null)
                {
                    synthesizer.SelectVoice(voice.VoiceInfo.Name);
                }
                else
                {
                    throw new InvalidOperationException($"没有找到支持{TTSOption.Instance.Language.Value}的语音。请检查是否安装了{TTSOption.Instance.Language.Value}TTS语音包。");
                }

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"初始化{TTSOption.Instance.Language.Value}语音失败:{ex.Message}", ex);
            }
        }

        public void Play(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("单词不能为空或仅为空格。", nameof(word));

            try
            {
                synthesizer.Speak(word);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("播放语音时发生错误。", ex);
            }
        }

        public void Dispose()
        {
            if (synthesizer != null)
            {
                synthesizer.Dispose();
                synthesizer = null;
            }
        }
    }
}
