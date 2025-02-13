﻿using Edge_tts_sharp;
using Edge_tts_sharp.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dictation.Core
{
    internal class EdgeTTSPlayer : ILanguageSettableTTSPlayer
    {
        private static readonly ReadOnlyDictionary<string, string> ttsMapping = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "ja","ja-JP-NanamiNeural" },
            { "zh","zh-CN-XiaoxiaoNeural" },
            { "en","en-US-AriaNeural" }
        });

        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        private eVoice voice;

        public void Dispose()
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
            voice = null;
        }

        public void SetLanguage(string language)
        {
            var voices = Edge_tts.GetVoice();

            voice = voices.FirstOrDefault(c => c.ShortName == ttsMapping[language]);
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
            }, tokenSource.Token).ConfigureAwait(false);
        }
    }
}
