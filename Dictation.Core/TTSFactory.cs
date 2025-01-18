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

        public ITTSPlayer CreateTTSPlayer(string target, string language)
        {
            var tts = serviceProvider.GetRequiredKeyedService<ILanguageSettableTTSPlayer>(target);
            tts.SetLanguage(language);

            return new TTSProxy(tts);
        }
    }
}
