using System.Net;
using Dictation.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dictation.Presentation.Mcp
{
    /// <summary>在唯一Generic Host内管理可选本机监听器，启动失败不影响WPF或TTS。</summary>
    /// <param name="settings">读取持久化开关和固定端口。</param>
    /// <param name="services">唯一Host容器，用于框架路由及请求作用域。</param>
    /// <param name="servers">创建由本监听器负责释放的Kestrel实例。</param>
    internal sealed class McpHostedService(McpSettingsService settings, IServiceProvider services,
        Func<int, KestrelServer> servers) : IHostedService, IMcpStatusReader, IMcpHostControl, IAsyncDisposable, IDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private KestrelServer? server;
        private McpStatusDto status = new(McpRunState.Disabled, Endpoint(47821));
        private bool stopped;
        private int disposed;

        /// <inheritdoc />
        public McpStatusDto GetStatus() => Volatile.Read(ref status);

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ApplyAsync(settings.Load(), cancellationToken);
            }
            catch (SettingsConfigurationException)
            {
                Volatile.Write(ref status, new(McpRunState.Failed, Endpoint(47821), "MCP配置无效，请在设置中修正。"));
            }
        }

        /// <inheritdoc />
        public async Task ApplyAsync(McpSettingsDto options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.Port is < 1024 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Port));
            }
            await gate.WaitAsync(cancellationToken);
            try
            {
                ObjectDisposedException.ThrowIf(disposed != 0, this);
                if (stopped)
                {
                    throw new InvalidOperationException("程序正在退出，不能重新开启MCP。");
                }
                var endpoint = Endpoint(options.Port);
                if (server is not null && options.Enabled && status.Endpoint == endpoint)
                {
                    return;
                }
                await CloseAsync();
                if (!options.Enabled)
                {
                    Volatile.Write(ref status, new(McpRunState.Disabled, endpoint));
                    return;
                }
                Volatile.Write(ref status, new(McpRunState.Starting, endpoint));
                try
                {
                    var candidate = servers(options.Port);
                    server = candidate;
                    var pipeline = new ApplicationBuilder(services, candidate.Features);
                    pipeline.Use(async (context, next) =>
                    {
                        // 固定本机地址不是认证；Host/Origin校验阻止跨站和DNS重绑定请求。
                        if (!IsAllowed(context.Request, options.Port))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return;
                        }
                        await next(context);
                    });
                    pipeline.UseRouting();
                    pipeline.UseEndpoints(endpoints => endpoints.MapMcp("/mcp"));
                    var application = new McpHttpApplication(services.GetRequiredService<IHttpContextFactory>(),
                        services.GetRequiredService<IServiceScopeFactory>(), pipeline.Build());
                    await candidate.StartAsync(application, cancellationToken);
                    Volatile.Write(ref status, new(McpRunState.Running, endpoint));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await CloseAsync();
                    Volatile.Write(ref status, new(McpRunState.Disabled, endpoint));
                    throw;
                }
                catch (Exception exception)
                {
                    services.GetRequiredService<ILogger<McpHostedService>>().LogError(exception, "本机MCP启动失败，界面仅显示简明状态。");
                    await CloseAsync();
                    Volatile.Write(ref status, new(McpRunState.Failed, endpoint, "MCP启动失败，端口可能已被占用；请修改端口或重试。"));
                }
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>验证完全匹配的本机Host及可选同源Origin，不开启CORS。</summary>
        /// <param name="request">尚未进入SDK的请求。</param>
        /// <param name="port">当前监听端口。</param>
        /// <returns>是否允许进入MCP处理。</returns>
        private static bool IsAllowed(HttpRequest request, int port) =>
            request.Host.Host == "127.0.0.1" && request.Host.Port == port
            && (!request.Headers.ContainsKey("Origin") || request.Headers.Origin.Count == 1
                && request.Headers.Origin[0] == $"http://127.0.0.1:{port}");

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                stopped = true;
                Volatile.Write(ref status, status with { State = McpRunState.Stopping });
                await CloseAsync();
                Volatile.Write(ref status, status with { State = McpRunState.Disabled });
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>关闭并释放本类拥有的监听器，即使停止失败也不留下socket。</summary>
        private async Task CloseAsync()
        {
            var current = server;
            server = null;
            if (current is null)
            {
                return;
            }
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await current.StopAsync(timeout.Token);
            }
            finally
            {
                current.Dispose();
            }
        }

        /// <summary>固定生成IPv4本机端点，设置不能扩大监听地址。</summary>
        /// <param name="port">有效高位端口。</param>
        private static Uri Endpoint(int port) => new($"http://127.0.0.1:{port}/mcp");

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }
            await StopAsync(CancellationToken.None);
        }

        /// <inheritdoc />
        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
