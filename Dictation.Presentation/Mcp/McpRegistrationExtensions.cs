using System.Net;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Dictation.Presentation.Mcp
{
    /// <summary>在已有服务集合注册HTTP适配，不建立第二容器或业务层。</summary>
    internal static class McpRegistrationExtensions
    {
        /// <summary>注册微软HTTP组件、官方协议实现及同一有状态监听器的能力别名。</summary>
        /// <param name="services">唯一Host的服务集合。</param>
        /// <returns>原服务集合。</returns>
        internal static IServiceCollection AddDictationMcp(this IServiceCollection services)
        {
            services.AddRouting();
            services.AddSingleton(_ => new DiagnosticListener("Microsoft.AspNetCore"));
            services.AddSingleton<IHttpContextFactory, DefaultHttpContextFactory>();
            services.AddSingleton<Func<int, KestrelServer>>(provider => port =>
            {
                var logger = provider.GetRequiredService<ILoggerFactory>();
                var options = new KestrelServerOptions { AddServerHeader = false };
                options.Listen(IPAddress.Loopback, port);
                options.Limits.MaxRequestBodySize = 1024 * 1024;
                return new KestrelServer(Options.Create(options), new SocketTransportFactory(Options.Create(new SocketTransportOptions()), logger), logger);
            });
            services.AddSingleton<McpHostedService>();
            services.AddSingleton<IMcpHostControl>(provider => provider.GetRequiredService<McpHostedService>());
            services.AddSingleton<IMcpStatusReader>(provider => provider.GetRequiredService<McpHostedService>());
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<McpHostedService>());
            services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
                .WithTools<DictationMcpTools>();
            return services;
        }
    }
}
