using Dictation.Presentation.Audio;
using NAudio.Wave;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证内存 WAV 播放器在完成、失败与取消路径释放播放器和输入流。
    /// </summary>
    public sealed class MemoryWavAudioPlayerTests
    {
        private static readonly byte[] OneSampleWav =
        {
            0x52, 0x49, 0x46, 0x46, 0x26, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x40, 0x1F, 0x00, 0x00, 0x80, 0x3E, 0x00, 0x00,
            0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        [Fact]
        public async Task PlaybackCompleted_UsesWaveFileReaderAndReleasesPlayerAndInput()
        {
            var output = new ControlledWavePlayer { CompleteWhenPlayed = true };
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);

            await player.PlayWavAsync(OneSampleWav, TestContext.Current.CancellationToken);

            var reader = Assert.IsType<WaveFileReader>(output.Provider);
            Assert.Equal(1, output.PlayCallCount);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        [Fact]
        public async Task PlaybackFailed_PropagatesDeviceErrorAndReleasesPlayerAndInput()
        {
            var failure = new InvalidOperationException("音频设备断开");
            var output = new ControlledWavePlayer { PlaybackFailure = failure };
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                player.PlayWavAsync(OneSampleWav, TestContext.Current.CancellationToken));

            Assert.Same(failure, actual);
            var reader = Assert.IsType<WaveFileReader>(output.Provider);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        [Fact]
        public async Task PlaybackCancelled_StopsPlaybackAndReleasesPlayerAndInput()
        {
            var output = new ControlledWavePlayer();
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var playback = player.PlayWavAsync(OneSampleWav, cancellation.Token);
            await output.Played.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => playback);
            var reader = Assert.IsType<WaveFileReader>(output.Provider);
            Assert.Equal(1, output.StopCallCount);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        [Fact]
        public async Task PlaybackCancellation_WhenStopFails_StillCancelsAndReleasesResources()
        {
            var output = new ControlledWavePlayer
            {
                StopFailure = new InvalidOperationException("设备停止失败")
            };
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var playback = player.PlayWavAsync(OneSampleWav, cancellation.Token);
            await output.Played.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            var cancelException = Record.Exception(cancellation.Cancel);

            Assert.Null(cancelException);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => playback);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        [Fact]
        public async Task CancellationDuringInit_DoesNotStartPlaybackAndReleasesResources()
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var output = new ControlledWavePlayer { OnInit = cancellation.Cancel };
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                player.PlayWavAsync(OneSampleWav, cancellation.Token));

            Assert.Equal(0, output.PlayCallCount);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SynchronousDeviceFailure_ReleasesPlayerAndInput(bool failDuringInit)
        {
            var failure = new InvalidOperationException("设备启动失败");
            var output = new ControlledWavePlayer
            {
                InitFailure = failDuringInit ? failure : null,
                PlayFailure = failDuringInit ? null : failure
            };
            var input = new TrackingMemoryStream(OneSampleWav);
            var player = new MemoryWavAudioPlayer(() => output, _ => input);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                player.PlayWavAsync(OneSampleWav, TestContext.Current.CancellationToken));

            Assert.Same(failure, actual);
            Assert.True(output.IsDisposed);
            Assert.True(input.IsDisposed);
        }

        private sealed class TrackingMemoryStream : MemoryStream
        {
            public TrackingMemoryStream(byte[] buffer)
                : base(buffer, writable: false)
            {
            }

            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class ControlledWavePlayer : IWavePlayer
        {
            public event EventHandler<StoppedEventArgs>? PlaybackStopped;

            public IWaveProvider? Provider { get; private set; }

            public bool CompleteWhenPlayed { get; init; }

            public Exception? PlaybackFailure { get; init; }

            public Exception? StopFailure { get; init; }

            public Exception? InitFailure { get; init; }

            public Exception? PlayFailure { get; init; }

            public Action? OnInit { get; init; }

            public int PlayCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public bool IsDisposed { get; private set; }

            public TaskCompletionSource Played { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public float Volume { get; set; } = 1;

            public PlaybackState PlaybackState { get; private set; }

            public WaveFormat OutputWaveFormat => Provider?.WaveFormat
                ?? WaveFormat.CreateIeeeFloatWaveFormat(8000, 1);

            public void Init(IWaveProvider waveProvider)
            {
                Provider = waveProvider;
                OnInit?.Invoke();
                if (InitFailure is not null) throw InitFailure;
            }

            public void Play()
            {
                PlayCallCount++;
                if (PlayFailure is not null) throw PlayFailure;
                PlaybackState = PlaybackState.Playing;
                Played.TrySetResult();
                if (PlaybackFailure is not null)
                {
                    PlaybackState = PlaybackState.Stopped;
                    PlaybackStopped?.Invoke(this, new StoppedEventArgs(PlaybackFailure));
                }
                else if (CompleteWhenPlayed)
                {
                    PlaybackState = PlaybackState.Stopped;
                    PlaybackStopped?.Invoke(this, new StoppedEventArgs());
                }
            }

            public void Pause()
            {
                PlaybackState = PlaybackState.Paused;
            }

            public void Stop()
            {
                StopCallCount++;
                if (StopFailure is not null)
                {
                    throw StopFailure;
                }
                PlaybackState = PlaybackState.Stopped;
                PlaybackStopped?.Invoke(this, new StoppedEventArgs());
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
