using Dictation.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    public sealed class LexiconLookupWindowFactory
    {
        private readonly IServiceProvider provider;

        public LexiconLookupWindowFactory(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public bool? ShowDialog(System.Windows.Window owner, LearningLanguage language)
        {
            ArgumentNullException.ThrowIfNull(owner);
            var window = provider.GetRequiredService<LexiconLookupWindow>();
            window.ViewModel.Initialize(language);
            window.Owner = owner;
            return window.ShowDialog();
        }
    }
}
