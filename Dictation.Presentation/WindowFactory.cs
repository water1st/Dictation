using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    public class WindowFactory
    {
        private readonly IServiceProvider provider;

        public WindowFactory(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public TWindow GetWindow<TWindow>() where TWindow : Form
        {
            return provider.GetRequiredService<TWindow>();
        }
    }
}
