using Dictation.Presentation.Theme;
using Dictation.Services.Configuration;

namespace Dictation.Tests.Presentation
{
    public sealed class ThemeManagerTests
    {
        [Fact]
        public void SystemMode_UsesInitialProviderThemeAndFollowsProviderChanges()
        {
            using var provider = new FakeSystemThemeProvider(ResolvedTheme.Dark);
            using var manager = new ThemeManager(provider);

            Assert.Equal(ThemeMode.System, manager.Mode);
            Assert.Equal(ResolvedTheme.Dark, manager.CurrentTheme);

            provider.ChangeTheme(ResolvedTheme.Light);

            Assert.Equal(ResolvedTheme.Light, manager.CurrentTheme);
        }

        [Fact]
        public void ManualMode_IgnoresProviderChanges()
        {
            using var provider = new FakeSystemThemeProvider(ResolvedTheme.Dark);
            using var manager = new ThemeManager(provider);
            manager.SetThemeMode(ThemeMode.Light);

            provider.ChangeTheme(ResolvedTheme.Dark);

            Assert.Equal(ThemeMode.Light, manager.Mode);
            Assert.Equal(ResolvedTheme.Light, manager.CurrentTheme);
        }

        [Fact]
        public void SwitchingBackToSystem_ImmediatelySynchronizesWithProvider()
        {
            using var provider = new FakeSystemThemeProvider(ResolvedTheme.Dark);
            using var manager = new ThemeManager(provider);
            manager.SetThemeMode(ThemeMode.Light);

            manager.SetThemeMode(ThemeMode.System);

            Assert.Equal(ResolvedTheme.Dark, manager.CurrentTheme);
        }

        [Fact]
        public void ThemeChanged_IsRaisedOnlyWhenResolvedThemeChanges()
        {
            using var provider = new FakeSystemThemeProvider(ResolvedTheme.Light);
            using var manager = new ThemeManager(provider);
            var changeCount = 0;
            manager.ThemeChanged += (_, _) => changeCount++;

            manager.SetThemeMode(ThemeMode.Dark);
            manager.SetThemeMode(ThemeMode.Dark);
            provider.ChangeTheme(ResolvedTheme.Dark);
            manager.SetThemeMode(ThemeMode.System);

            Assert.Equal(2, changeCount);
        }

        [Fact]
        public void UndefinedThemeMode_ThrowsArgumentOutOfRangeException()
        {
            using var provider = new FakeSystemThemeProvider(ResolvedTheme.Light);
            using var manager = new ThemeManager(provider);

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                manager.SetThemeMode((ThemeMode)99));

            Assert.Contains("主题", exception.Message);
        }

        [Fact]
        public void Dispose_UnsubscribesAndDisposesProvider()
        {
            var provider = new FakeSystemThemeProvider(ResolvedTheme.Light);
            var manager = new ThemeManager(provider);
            manager.Dispose();

            provider.ChangeTheme(ResolvedTheme.Dark);

            Assert.Equal(ResolvedTheme.Light, manager.CurrentTheme);
            Assert.True(provider.IsDisposed);
            Assert.False(provider.HasSubscribers);
        }

        [Fact]
        public void WindowsProvider_UnknownOrFailingRegistryValueFallsBackToLight()
        {
            using var unknownProvider = new WindowsSystemThemeProvider(() => 7, subscribeToSystemEvents: false);
            using var failingProvider = new WindowsSystemThemeProvider(
                () => throw new InvalidOperationException("registry unavailable"),
                subscribeToSystemEvents: false);

            Assert.Equal(ResolvedTheme.Light, unknownProvider.CurrentTheme);
            Assert.Equal(ResolvedTheme.Light, failingProvider.CurrentTheme);
        }

        private sealed class FakeSystemThemeProvider : ISystemThemeProvider
        {
            private EventHandler? themeChanged;

            public FakeSystemThemeProvider(ResolvedTheme initialTheme)
            {
                CurrentTheme = initialTheme;
            }

            public ResolvedTheme CurrentTheme { get; private set; }

            public event EventHandler? ThemeChanged
            {
                add => themeChanged += value;
                remove => themeChanged -= value;
            }

            public bool IsDisposed { get; private set; }

            public bool HasSubscribers => themeChanged is not null;

            public void ChangeTheme(ResolvedTheme theme)
            {
                CurrentTheme = theme;
                themeChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Dispose()
            {
                IsDisposed = true;
                themeChanged = null;
            }
        }
    }
}
