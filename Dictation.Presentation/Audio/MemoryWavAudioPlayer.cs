using NAudio.Wave;

namespace Dictation.Presentation.Audio
{
    /// <summary>
    /// 使用 Windows WaveOut 设备直接播放内存 WAV 数据。
    /// </summary>
    internal sealed class MemoryWavAudioPlayer : IAudioPlayer
    {
        private readonly Func<IWavePlayer> wavePlayerFactory;
        private readonly Func<ReadOnlyMemory<byte>, MemoryStream> streamFactory;

        /// <summary>
        /// 创建使用默认 WaveOut 设备的内存播放器。
        /// </summary>
        public MemoryWavAudioPlayer()
            : this(
                static () => new WaveOut(),
                static data => new MemoryStream(data.ToArray(), writable: false))
        {
        }

        internal MemoryWavAudioPlayer(
            Func<IWavePlayer> wavePlayerFactory,
            Func<ReadOnlyMemory<byte>, MemoryStream>? streamFactory = null)
        {
            this.wavePlayerFactory = wavePlayerFactory
                ?? throw new ArgumentNullException(nameof(wavePlayerFactory));
            this.streamFactory = streamFactory
                ?? (static data => new MemoryStream(data.ToArray(), writable: false));
        }

        /// <inheritdoc />
        public async Task PlayWavAsync(
            ReadOnlyMemory<byte> wavData,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = streamFactory(wavData);
            using var reader = new WaveFileReader(stream);
            using var player = wavePlayerFactory();
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<StoppedEventArgs> stopped = (_, args) =>
            {
                if (args.Exception is null)
                {
                    completion.TrySetResult();
                }
                else
                {
                    completion.TrySetException(args.Exception);
                }
            };
            player.PlaybackStopped += stopped;
            try
            {
                player.Init(reader);
                using var registration = cancellationToken.Register(() =>
                {
                    completion.TrySetCanceled(cancellationToken);
                    player.Stop();
                });
                player.Play();
                await completion.Task;
            }
            finally
            {
                player.PlaybackStopped -= stopped;
            }
        }
    }
}
