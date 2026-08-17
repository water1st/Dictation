using System.IO.Pipes;
using Dictation.Services;
using Dictation.Services.SingleInstance;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Services
{
    public sealed class SingleInstanceCoordinatorTests
    {
        [Fact]
        public void TryAcquirePrimaryInstance_OnlyFirstCoordinatorBecomesPrimary()
        {
            var names = CreateNames();
            using var first = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var second = new SingleInstanceCoordinator(names.MutexName, names.PipeName);

            Assert.True(first.TryAcquirePrimaryInstance());
            Assert.False(second.TryAcquirePrimaryInstance());
            Assert.True(first.TryAcquirePrimaryInstance());
        }

        [Fact]
        public void TryAcquirePrimaryInstance_AfterPrimaryAndCandidateAreDisposed_AllowsNewPrimary()
        {
            var names = CreateNames();
            using (var first = new SingleInstanceCoordinator(names.MutexName, names.PipeName))
            using (var second = new SingleInstanceCoordinator(names.MutexName, names.PipeName))
            {
                Assert.True(first.TryAcquirePrimaryInstance());
                Assert.False(second.TryAcquirePrimaryInstance());
            }

            using var replacement = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(replacement.TryAcquirePrimaryInstance());
        }

        [Fact]
        public void TryAcquirePrimaryInstance_AfterFailure_RemainsFalse()
        {
            var names = CreateNames();
            using var first = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var second = new SingleInstanceCoordinator(names.MutexName, names.PipeName);

            Assert.True(first.TryAcquirePrimaryInstance());
            Assert.False(second.TryAcquirePrimaryInstance());
            first.Dispose();

            Assert.False(second.TryAcquirePrimaryInstance());
        }

        [Fact]
        public void Constructor_RejectsInvalidNames()
        {
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator("", "pipe"));
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator("mutex", ""));
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator("mutex", "pipe\\name"));
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator("mutex", "pipe/name"));
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator(null!, "pipe"));
            Assert.Throws<ArgumentException>(() => new SingleInstanceCoordinator("mutex", null!));
        }

        [Fact]
        public async Task StartListeningAsync_WhenNotPrimary_Throws()
        {
            var names = CreateNames();
            using var first = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var second = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(first.TryAcquirePrimaryInstance());
            Assert.False(second.TryAcquirePrimaryInstance());

            await Assert.ThrowsAsync<InvalidOperationException>(() => second.StartListeningAsync(
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void StartListeningAsync_WhenCalledTwice_ReturnsSameTask()
        {
            var names = CreateNames();
            using var coordinator = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(coordinator.TryAcquirePrimaryInstance());

            var firstStart = coordinator.StartListeningAsync(TestContext.Current.CancellationToken);
            var secondStart = coordinator.StartListeningAsync(TestContext.Current.CancellationToken);

            Assert.Same(firstStart, secondStart);
        }

        [Fact]
        public async Task StartListeningAsync_AndSignal_RaisesActivationWithoutArguments()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            var activation = NewSignalSource();
            primary.ActivationRequested += activation.Handler;
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(1, activation.Count);
        }

        [Fact]
        public async Task SignalPrimaryInstanceAsync_AllowsConsecutiveSignals()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            var activation = NewSignalSource(expectedCount: 2);
            primary.ActivationRequested += activation.Handler;
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(2, activation.Count);
        }

        [Fact]
        public async Task Listener_IgnoresInvalidAndEmptyPayloads_ThenAcceptsValidSignal()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            var activation = NewSignalSource();
            primary.ActivationRequested += activation.Handler;
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            await SendPayloadAsync(names.PipeName, new byte[] { 0x7f });
            await SendPayloadAsync(names.PipeName, Array.Empty<byte>());
            Assert.Equal(0, activation.Count);

            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(1, activation.Count);
        }

        [Fact]
        public async Task Listener_WhenClientSendsNothing_ContinuesAfterReadTimeout()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            var activation = NewSignalSource();
            primary.ActivationRequested += activation.Handler;
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            await using (var stalledClient = new NamedPipeClientStream(
                ".",
                names.PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous))
            {
                await stalledClient.ConnectAsync(2000, TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<TimeoutException>(() => activation.Task.WaitAsync(
                    TimeSpan.FromMilliseconds(500),
                    TestContext.Current.CancellationToken));
            }

            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(1, activation.Count);
        }

        [Fact]
        public async Task TrySignalPrimaryInstanceAsync_WhenNoListener_ReturnsFalse()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            Assert.False(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromMilliseconds(100),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task TrySignalPrimaryInstanceAsync_PropagatesExternalCancellation()
        {
            var names = CreateNames();
            using var coordinator = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(1),
                cancellation.Token));
        }

        [Fact]
        public async Task TrySignalPrimaryInstanceAsync_RejectsNonPositiveTimeout()
        {
            var names = CreateNames();
            using var coordinator = new SingleInstanceCoordinator(names.MutexName, names.PipeName);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.TrySignalPrimaryInstanceAsync(
                TimeSpan.Zero,
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromMilliseconds(-1),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Dispose_StopsListener_ReleasesMutex_AndIsIdempotent()
        {
            var names = CreateNames();
            var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            primary.Dispose();
            primary.Dispose();

            Assert.Throws<ObjectDisposedException>(() => primary.TryAcquirePrimaryInstance());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => primary.StartListeningAsync(
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => primary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromMilliseconds(100),
                TestContext.Current.CancellationToken));

            using var replacement = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(replacement.TryAcquirePrimaryInstance());
        }

        [Fact]
        public async Task ActivationSubscriberException_DoesNotStopLaterSignals()
        {
            var names = CreateNames();
            using var primary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            using var secondary = new SingleInstanceCoordinator(names.MutexName, names.PipeName);
            Assert.True(primary.TryAcquirePrimaryInstance());
            Assert.False(secondary.TryAcquirePrimaryInstance());

            var activation = NewSignalSource(expectedCount: 2);
            primary.ActivationRequested += () => throw new InvalidOperationException("测试订阅者异常");
            primary.ActivationRequested += activation.Handler;
            _ = primary.StartListeningAsync(TestContext.Current.CancellationToken);

            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            Assert.True(await secondary.TrySignalPrimaryInstanceAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken));
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(2, activation.Count);
        }

        [Fact]
        public void AddDictationServices_RegistersSingleInstanceCoordinatorAsSingleton()
        {
            using var provider = new ServiceCollection()
                .AddDictationServices()
                .BuildServiceProvider();

            var first = provider.GetRequiredService<SingleInstanceCoordinator>();
            var second = provider.GetRequiredService<SingleInstanceCoordinator>();

            Assert.Same(first, second);
        }

        private static (string MutexName, string PipeName) CreateNames()
        {
            var suffix = Guid.NewGuid().ToString("N");
            return ($"Local\\Dictation.Tests.{suffix}", $"Dictation.Tests.{suffix}");
        }

        private static SignalSource NewSignalSource(int expectedCount = 1)
        {
            return new SignalSource(expectedCount);
        }

        private static async Task SendPayloadAsync(string pipeName, byte[] payload)
        {
            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(2000, TestContext.Current.CancellationToken);
            if (payload.Length > 0)
            {
                await client.WriteAsync(payload, TestContext.Current.CancellationToken);
                await client.FlushAsync(TestContext.Current.CancellationToken);
            }
        }

        private sealed class SignalSource
        {
            private readonly TaskCompletionSource<bool> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly int expectedCount;
            private int count;

            public SignalSource(int expectedCount)
            {
                this.expectedCount = expectedCount;
            }

            public Action Handler => OnActivation;

            public int Count => Volatile.Read(ref count);

            public Task Task => completion.Task;

            private void OnActivation()
            {
                var currentCount = Interlocked.Increment(ref count);
                if (currentCount >= expectedCount)
                {
                    completion.TrySetResult(true);
                }
            }
        }
    }
}
