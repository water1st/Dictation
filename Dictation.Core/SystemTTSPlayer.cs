﻿using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace Dictation.Core
{
    internal class SystemTTSPlayer : ILanguageSettableTTSPlayer
    {
        private SpeechSynthesizer synthesizer;
        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        public SystemTTSPlayer()
        {
            synthesizer = new SpeechSynthesizer();

            // 设置语速和音量
            synthesizer.Rate = 0; // 语速：-10 (最慢) 到 10 (最快)
            synthesizer.Volume = 100; // 音量：0 到 100
        }

        public void Play(string word)
        {
            try
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    synthesizer.Speak(word);
                }, tokenSource.Token).ConfigureAwait(false);
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
                tokenSource.Cancel();
                tokenSource.Dispose();
                synthesizer.Dispose();
                synthesizer = null;
            }
        }

        public void SetLanguage(string language)
        {
            try
            {
                var voices = synthesizer.GetInstalledVoices();

                var voice = voices.FirstOrDefault(v => v?.VoiceInfo?.Culture?.TwoLetterISOLanguageName == language);

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
    }
}
