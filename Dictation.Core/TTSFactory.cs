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

        public ITTSPlayer CreateTTSPlayer()
        {
            var key = TTSOption.Instance.Language.Key.Split('_')[0];

            var tts = serviceProvider.GetRequiredKeyedService<ITTSPlayer>(key);
            return new TTSProxy(tts);
        }
    }
}
