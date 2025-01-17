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
            var tts = serviceProvider.GetRequiredKeyedService<ITTSPlayer>(TTSOption.Instance.Target);
            return new TTSProxy(tts);
        }
    }
}
