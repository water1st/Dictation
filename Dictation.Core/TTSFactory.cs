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
            return serviceProvider.GetRequiredKeyedService<ITTSPlayer>(TTSOption.Instance.Language.Key);
        }
    }
}
