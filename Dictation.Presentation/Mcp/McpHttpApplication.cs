using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation.Mcp
{
    /// <summary>把Kestrel公共请求合同委派给微软HTTP上下文与SDK路由，复用唯一容器。</summary>
    /// <param name="contexts">框架HTTP上下文工厂。</param>
    /// <param name="scopes">唯一容器的作用域工厂。</param>
    /// <param name="pipeline">微软路由与官方SDK构建的请求委派。</param>
    internal sealed class McpHttpApplication(IHttpContextFactory contexts, IServiceScopeFactory scopes, RequestDelegate pipeline)
        : IHttpApplication<McpHttpApplication.RequestContext>
    {
        /// <inheritdoc />
        public RequestContext CreateContext(IFeatureCollection features)
        {
            var context = contexts.Create(features);
            var scope = scopes.CreateScope();
            context.RequestServices = scope.ServiceProvider;
            return new(context, scope);
        }

        /// <inheritdoc />
        public Task ProcessRequestAsync(RequestContext context) => pipeline(context.Context);

        /// <inheritdoc />
        public void DisposeContext(RequestContext context, Exception? exception)
        {
            try
            {
                contexts.Dispose(context.Context);
            }
            finally
            {
                context.Scope.Dispose();
            }
        }

        /// <summary>只属于一次HTTP请求的上下文和依赖作用域。</summary>
        /// <param name="Context">本请求的HTTP上下文。</param>
        /// <param name="Scope">本请求拥有的依赖作用域。</param>
        internal sealed record RequestContext(HttpContext Context, IServiceScope Scope);
    }
}
