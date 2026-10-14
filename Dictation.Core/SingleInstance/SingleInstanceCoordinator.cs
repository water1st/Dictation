using System.IO;
using System.IO.Pipes;

namespace Dictation.Core.SingleInstance
{
    /// <summary>
    /// 以当前会话互斥体确定主实例，并通过当前用户管道转发激活请求。
    /// </summary>
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

        /// <summary>
        /// 以当前会话互斥体确定主实例，并通过当前用户管道转发激活请求。
        /// </summary>
        public SingleInstanceCoordinator()
            : this(DefaultMutexName, DefaultPipeName)
        {
        }

        /// <summary>
        /// 以当前会话互斥体确定主实例，并通过当前用户管道转发激活请求。
        /// </summary>
        /// <param name="mutexName">用于区分主实例的会话互斥体名称。</param>
        /// <param name="pipeName">用于转发激活请求的管道名称，不含路径分隔符。</param>
        public SingleInstanceCoordinator(string mutexName, string pipeName)
        {
            this.mutexName = ValidateMutexName(mutexName);
            this.pipeName = ValidatePipeName(pipeName);
        }

        public event Action? ActivationRequested;

        /// <summary>
        /// 仅尝试一次创建会话互斥体，后续调用返回已确定的主实例身份。
        /// </summary>
        /// <returns>当前对象是否取得主实例身份。</returns>
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

        /// <summary>
        /// 由主实例启动激活监听；重复调用返回同一监听任务。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        /// <returns>持续到取消或释放的监听任务；不是仅表示已启动的任务。</returns>
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

        /// <summary>
        /// 在限定时间内向主实例发送激活信号；连接失败不阻止次实例退出。
        /// </summary>
        /// <param name="timeout">连接及发送的总超时，必须大于零。</param>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        /// <returns>信号已发送时为真；超时或连接失败时为假，调用方取消仍传播。</returns>
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

        /// <inheritdoc />
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

        /// <summary>
        /// 只接受当前用户的单字节激活消息；连接读取有短超时，异常后继续监听直至取消。
        /// </summary>
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
