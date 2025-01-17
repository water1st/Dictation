using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Dictation.Core
{
    public class TTSFactory
    {
        private readonly IServiceProvider serviceProvider;
        private static readonly ConcurrentDictionary<string, Type> ttsProviderMap = new ConcurrentDictionary<string, Type>();

        public TTSFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public ITTSPlayer CreateTTSPlayer()
        {
            var key = TTSOption.Instance.Language.Key;


            var type = ttsProviderMap.GetOrAdd(key, (k) =>
            {
                var args = key.Split("_");
                var target = (TTSTarget)(Convert.ToInt32(args[0]));
                var language = args[1];

                return Assembly.GetExecutingAssembly().GetTypes()
                .Where(c => c.IsClass && typeof(ITTSPlayer).IsAssignableFrom(c))
                .FirstOrDefault(c =>
                {
                    var attr = c.GetCustomAttribute<TTSAttribute>();
                    return attr != null && attr.Target == target && (attr.Target == TTSTarget.System || 
                    (attr.Target == TTSTarget.Edge && attr.Language == language));
                });
            });

            var tts = (ITTSPlayer)serviceProvider.GetRequiredService(type);
            return new TTSProxy(tts);
        }
    }
}
