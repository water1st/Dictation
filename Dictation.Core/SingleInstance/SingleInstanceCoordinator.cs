using System.IO;
using System.IO.Pipes;

namespace Dictation.Core.SingleInstance
{
    public sealed class SingleInstanceCoordinator : IDisposable
    {
        private const byte ActivationCode = 0x01;
        private const string DefaultMutexName = "Local\\Dictation";
        private const string DefaultPipeName = "Dictation.Activation";
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan ListenerRetryDelay = TimeSpan.FromMilliseconds(25);
        private readonly object syncRoot = new();
        private readonly string mutexName;
        private readonly string pipeName;
        private Mutex? instanceMutex;
        private CancellationTokenSource? listenerCancellation;
        private Task? listenerTask;
        private bool acquisitionAttempted;
        private bool isPrimaryInstance;
        private bool disposed;

        public SingleInstanceCoordinator()
            : this(DefaultMutexName, DefaultPipeName)
        {
        }

        public SingleInstanceCoordinator(string mutexName, string pipeName)
        {
            this.mutexName = ValidateMutexName(mutexName);
            this.pipeName = ValidatePipeName(pipeName);
        }

        public event Action? ActivationRequested;

        public bool TryAcquirePrimaryInstance()
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();

                if (acquisitionAttempted)
                {
                    return isPrimaryInstance;
                }

                acquisitionAttempted = true;
                var candidateMutex = new Mutex(initiallyOwned: false, mutexName, out var createdNew);
                if (!createdNew)
                {
                    candidateMutex.Dispose();
                    return false;
                }

                instanceMutex = candidateMutex;
                isPrimaryInstance = true;
                return true;
            }
        }

        public Task StartListeningAsync(CancellationToken cancellationToken = default)
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();

                if (!isPrimaryInstance)
                {
                    throw new InvalidOperationException("只有主实例可以启动激活监听。");
                }

                if (listenerTask is not null)
                {
                    return listenerTask;
                }

                listenerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                listenerTask = ListenLoopAsync(listenerCancellation.Token);
                return listenerTask;
            }
        }

        public async Task<bool> TrySignalPrimaryInstanceAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "超时时间必须大于零。");
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);

            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(timeoutCancellation.Token).ConfigureAwait(false);
                await client.WriteAsync(new[] { ActivationCode }, timeoutCancellation.Token).ConfigureAwait(false);
                await client.FlushAsync(timeoutCancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            Task? taskToWait;
            CancellationTokenSource? cancellationToDispose;
            Mutex? mutexToDispose;

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ActivationRequested = null;
                listenerCancellation?.Cancel();
                taskToWait = listenerTask;
                cancellationToDispose = listenerCancellation;
                mutexToDispose = instanceMutex;
                listenerCancellation = null;
                instanceMutex = null;
                isPrimaryInstance = false;
            }

            if (taskToWait is not null && taskToWait.Id != Task.CurrentId)
            {
                try
                {
                    taskToWait.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
            }

            cancellationToDispose?.Dispose();
            mutexToDispose?.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    var payload = new byte[1];
                    var bytesRead = await server.ReadAsync(payload, cancellationToken)
                        .AsTask()
                        .WaitAsync(ReadTimeout, cancellationToken)
                        .ConfigureAwait(false);
                    if (bytesRead == 1 && payload[0] == ActivationCode)
                    {
                        RaiseActivationRequested();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (TimeoutException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        await Task.Delay(ListenerRetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        private void RaiseActivationRequested()
        {
            var handlers = ActivationRequested?.GetInvocationList();
            if (handlers is null)
            {
                return;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception)
                {
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SingleInstanceCoordinator));
            }
        }

        private static string ValidateMutexName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("互斥体名称不能为空。", nameof(name));
            }

            return name;
        }

        private static string ValidatePipeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("管道名称不能为空。", nameof(name));
            }

            if (name.Contains('\\') || name.Contains('/'))
            {
                throw new ArgumentException("管道名称不能包含路径分隔符。", nameof(name));
            }

            return name;
        }
    }
}
