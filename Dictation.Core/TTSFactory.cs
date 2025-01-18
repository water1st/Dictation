using Microsoft.Extensions.DependencyInjection;
using System;

namespace Dictation.Core
{
    public class TTSFactory
    {
        private readonly IServiceProvider serviceProvider;

        public TTSFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public ITTSPlayer CreateTTSPlayer(string language)
        {
            var tts = serviceProvider.GetRequiredKeyedService<ILanguageSettableTTSPlayer>(TTSOption.Instance.Target);
            tts.SetLanguage(language);

            return new TTSProxy(tts);
        }
    }
}
