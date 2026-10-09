using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Startup;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Mcp;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dictation.Tests.Mcp
{
    /// <summary>真实TCP与SDK客户端验证同容器导入、来源限制及监听生命周期。</summary>
    public sealed class McpHttpIntegrationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData("Japanese", "食べます", "jmdict:1:1", "吃")]
        [InlineData("English", "went", "go%2:01:00::", "去")]
        public async Task SdkDiscoveryAndImport_StagesWholeGroupAndRequiresUserDecision(string language, string query, string sense, string meaning)
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            Assert.Equal(McpRunState.Running, environment.Status.GetStatus().State);
            await using var transport = new HttpClientTransport(new() { Endpoint = environment.Endpoint, TransportMode = HttpTransportMode.StreamableHttp });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: Token);
            var tools = await client.ListToolsAsync(cancellationToken: Token);
            Assert.Equal(new[] { "dictation_capabilities", "dictation_diagnostics", "dictation_import_prepare", "dictation_import_stage", "dictation_import_status", "dictation_lexicon_search" }, tools.Select(tool => tool.Name).Order().ToArray());
            Assert.All(tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description)));
            Assert.False(tools.Single(tool => tool.Name == "dictation_import_stage").JsonSchema.GetProperty("properties").TryGetProperty("sourceKind", out _));
            var lookup = await CallAsync(client, "dictation_lexicon_search", new() { ["language"] = language, ["query"] = query });
            Assert.Contains(lookup.EnumerateArray(), entry => entry.GetProperty("sourceSenseId").GetString() == sense
                && entry.GetProperty("meanings").EnumerateArray().Any(item => item.GetString()!.Contains(meaning, StringComparison.Ordinal)));
            var prepared = await CallAsync(client, "dictation_import_prepare", new() { ["language"] = language, ["text"] = query + "\nunknown_zzzz" });
            Assert.Equal(2, prepared.GetProperty("lines").GetArrayLength());
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
            var arguments = new Dictionary<string, object?> { ["language"] = language, ["selections"] = new[] { new ImportSelectionDto(query, sense) } };
            var staged = await CallAsync(client, "dictation_import_stage", arguments);
            Assert.Equal(1, staged.GetProperty("addedCount").GetInt32());
            var batchId = staged.GetProperty("batch").GetProperty("batchId").GetString()!;
            var batch = await environment.Host.Services.GetRequiredService<IImportBatchReader>().GetBatchAsync(batchId, Token);
            Assert.Equal(ImportSourceKind.Mcp, batch.SourceKind);
            Assert.Equal(ImportBatchStatus.Pending, batch.Status);
            var group = Assert.Single(batch.Items).WordGroup!;
            Assert.Equal(language == "Japanese" ? 1 : 2, group.Members.Count);
            Assert.All(group.Members, member => Assert.NotEmpty(member.Meanings));
            Assert.Contains(group.Members.SelectMany(member => member.Forms), form => form.Surface == (language == "Japanese" ? "食べた" : "gone"));
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM WordGroups WHERE Placement IN ('Learning','Review');"));
            var repeated = await CallAsync(client, "dictation_import_stage", arguments);
            Assert.Equal(0, repeated.GetProperty("addedCount").GetInt32());
            Assert.Equal(1, repeated.GetProperty("duplicateCount").GetInt32());
            var status = await CallAsync(client, "dictation_import_status", new() { ["batchId"] = batchId });
            Assert.Equal("Pending", status.GetProperty("status").GetString());
            await environment.Host.Services.GetRequiredService<IImportInboxActions>().DecideAsync(new(batchId, batch.Items.Select(item => item.ItemId).ToArray(), ImportDecision.Discard), Token);
            status = await CallAsync(client, "dictation_import_status", new() { ["batchId"] = batchId });
            Assert.Equal("Completed", status.GetProperty("status").GetString());
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
        }

        [Theory]
        [InlineData("evil.test", null)]
        [InlineData("localhost", null)]
        [InlineData("127.0.0.1:1", null)]
        [InlineData(null, "https://evil.test")]
        [InlineData(null, "null")]
        [InlineData(null, "http://localhost")]
        [InlineData(null, "http://127.0.0.1:1")]
        [InlineData(null, "")]
        public async Task WrongHostOrOrigin_IsRejectedBeforeAnyToolWrite(string? host, string? origin)
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, environment.Endpoint)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dictation_import_stage\",\"arguments\":{\"language\":\"Japanese\",\"selections\":[{\"sourceText\":\"食べる\",\"sourceSenseId\":\"jmdict:1:1\"}]}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json"); request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
            if (host is not null) request.Headers.Host = host;
            if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
            using var response = await client.SendAsync(request, Token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
        }

        [Fact]
        public async Task DisabledConflictRetryAndShutdown_DoNotLeakPortOrAbortHost()
        {
            await using var environment = await EnvironmentAsync();
            Assert.Equal(McpRunState.Disabled, environment.Status.GetStatus().State);
            using var owner = new TcpListener(IPAddress.Loopback, environment.Port);
            owner.Start();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            Assert.Equal(McpRunState.Failed, environment.Status.GetStatus().State);
            Assert.NotNull(environment.Status.GetStatus().ErrorMessage);
            Assert.DoesNotContain(environment.Directory.Path, environment.Status.GetStatus().ErrorMessage!, StringComparison.OrdinalIgnoreCase);
            owner.Stop();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            Assert.Equal(McpRunState.Running, environment.Status.GetStatus().State);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => environment.Control.ApplyAsync(new(false, environment.Port), new CancellationToken(true)));
            Assert.Equal(McpRunState.Running, environment.Status.GetStatus().State);
            await environment.Control.ApplyAsync(new(false, environment.Port), Token);
            owner.Start(); owner.Stop();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            await environment.Host.StopAsync(Token);
            owner.Start(); owner.Stop();
        }

        [Fact]
        public async Task SameOriginAndInvalidParameters_UseSdkErrorsWithoutWritingUserProgress()
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            await using var transport = new HttpClientTransport(new() { Endpoint = environment.Endpoint, AdditionalHeaders = new Dictionary<string, string> { ["Origin"] = $"http://127.0.0.1:{environment.Port}" } });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: Token);
            foreach (var arguments in new Dictionary<string, object?>[]
            {
                new() { ["language"] = "Japanese", ["selections"] = Array.Empty<ImportSelectionDto>() },
                new() { ["language"] = "Japanese", ["selections"] = new[] { new ImportSelectionDto("went", "go%2:01:00::") } },
                new() { ["language"] = "English", ["selections"] = Enumerable.Repeat(new ImportSelectionDto("went", "go%2:01:00::"), 101).ToArray() }
            })
            {
                var error = await client.CallToolAsync("dictation_import_stage", arguments, cancellationToken: Token);
                Assert.True(error.IsError);
                Assert.DoesNotContain(environment.Directory.Path, string.Join("", error.Content.OfType<TextContentBlock>().Select(item => item.Text)), StringComparison.OrdinalIgnoreCase);
            }
            Assert.True((await client.CallToolAsync("dictation_import_status", new Dictionary<string, object?> { ["batchId"] = "unknown" }, cancellationToken: Token)).IsError);
            await Assert.ThrowsAnyAsync<Exception>(() => client.CallToolAsync("dictation_confirm", new Dictionary<string, object?>(), cancellationToken: Token).AsTask());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CallToolAsync("dictation_import_stage", new Dictionary<string, object?>(), cancellationToken: new CancellationToken(true)).AsTask());
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
        }

        private static async Task<JsonElement> CallAsync(McpClient client, string name, Dictionary<string, object?> arguments)
        {
            var result = await client.CallToolAsync(name, arguments, cancellationToken: Token);
            Assert.NotEqual(true, result.IsError);
            using var document = JsonDocument.Parse(Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
            return document.RootElement.Clone();
        }

        [Fact]
        public async Task MultipleOriginsAndCrossSitePreflight_DoNotReachTools()
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            using var client = new HttpClient();
            using var multiple = new HttpRequestMessage(HttpMethod.Post, environment.Endpoint) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            multiple.Headers.TryAddWithoutValidation("Origin", new[] { $"http://127.0.0.1:{environment.Port}", "https://evil.test" });
            using var response = await client.SendAsync(multiple, Token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var preflight = new HttpRequestMessage(HttpMethod.Options, environment.Endpoint);
            preflight.Headers.Add("Origin", "https://evil.test");
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            using var denied = await client.SendAsync(preflight, Token);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
        }

        [Fact]
        public async Task OversizedRequest_IsRejectedBeforeStaging()
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, environment.Endpoint)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dictation_import_stage\",\"arguments\":{\"language\":\"Japanese\",\"selections\":[{\"sourceText\":\"食べる\",\"sourceSenseId\":\"jmdict:1:1\"}],\"padding\":\"" + new string('x', 1024 * 1024) + "\"}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
            using var response = await client.SendAsync(request, Token);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
        }

        [Fact]
        public async Task EnabledStartup_BindsAfterDatabaseInitializationAndSupportsPortChange()
        {
            await using var environment = await EnvironmentAsync(port => $"{{\"Mcp\":{{\"Enabled\":true,\"Port\":{port}}}}}");
            Assert.Equal(McpRunState.Running, environment.Status.GetStatus().State);
            await using var transport = new HttpClientTransport(new() { Endpoint = environment.Endpoint });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: Token);
            Assert.NotEmpty(await client.ListToolsAsync(cancellationToken: Token));
            using var next = new TcpListener(IPAddress.Loopback, 0);
            next.Start();
            var nextPort = ((IPEndPoint)next.LocalEndpoint).Port;
            next.Stop();
            await environment.Control.ApplyAsync(new(true, nextPort), Token);
            Assert.Equal(nextPort, environment.Status.GetStatus().Endpoint.Port);
            using var previous = new TcpListener(IPAddress.Loopback, environment.Port);
            previous.Start();
            previous.Stop();
            await using var nextTransport = new HttpClientTransport(new() { Endpoint = environment.Status.GetStatus().Endpoint });
            await using var nextClient = await McpClient.CreateAsync(nextTransport, cancellationToken: Token);
            Assert.NotEmpty(await nextClient.ListToolsAsync(cancellationToken: Token));
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{\"Mcp\":{\"Enabled\":\"invalid\"}}")]
        [InlineData("{\"Mcp\":{\"Port\":1}}")]
        public async Task BrokenConfiguration_DoesNotAbortHostAndCanBeRepaired(string json)
        {
            await using var environment = await EnvironmentAsync(_ => json);
            Assert.Equal(McpRunState.Failed, environment.Status.GetStatus().State);
            Assert.DoesNotContain(environment.Directory.Path, environment.Status.GetStatus().ErrorMessage!, StringComparison.OrdinalIgnoreCase);
            var settings = environment.Host.Services.GetRequiredService<McpSettingsService>();
            await settings.SaveAsync(new(true, environment.Port), Token);
            await environment.Control.ApplyAsync(settings.Load(), Token);
            Assert.Equal(McpRunState.Running, environment.Status.GetStatus().State);
        }

        [Fact]
        public async Task IncompleteRequest_ShutdownReleasesPortWithoutWritingBatch()
        {
            await using var environment = await EnvironmentAsync();
            await environment.Control.ApplyAsync(new(true, environment.Port), Token);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, environment.Port, Token);
            var bytes = Encoding.ASCII.GetBytes($"POST /mcp HTTP/1.1\r\nHost: 127.0.0.1:{environment.Port}\r\nContent-Type: application/json\r\nAccept: application/json, text/event-stream\r\nMCP-Protocol-Version: 2025-11-25\r\nContent-Length: 1000\r\n\r\n{{");
            await client.GetStream().WriteAsync(bytes, Token);
            await environment.Host.StopAsync(Token).WaitAsync(TimeSpan.FromSeconds(12), Token);
            using var owner = new TcpListener(IPAddress.Loopback, environment.Port);
            owner.Start();
            Assert.Equal(0L, await environment.ScalarAsync("SELECT COUNT(*) FROM ImportBatches;"));
        }

        private static async Task<McpEnvironment> EnvironmentAsync(Func<int, string>? configuration = null)
        {
            var directory = new TemporaryDirectory();
            var paths = new ApplicationPaths(directory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            using var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            if (configuration is not null)
            {
                Directory.CreateDirectory(paths.Config);
                await File.WriteAllTextAsync(Path.Combine(paths.Config, "mcp.json"), configuration(port), Token);
            }
            var host = Host.CreateDefaultBuilder().ConfigureAppConfiguration(builder => builder.SetBasePath(directory.Path)
                .AddDictationMcpConfiguration()).ConfigureServices(services => services
                    .AddDictationDataAccess(directory.Path).AddDictationCore().AddDictationMcp()).Build();
            await host.Services.GetRequiredService<StartupInitializer>().InitializeAsync(Token);
            await host.StartAsync(Token);
            return new(host, directory, port);
        }

        /// <summary>持有测试专属Host和临时数据库，不接触本机正式资源或配置。</summary>
        private sealed class McpEnvironment(IHost host, TemporaryDirectory directory, int port) : IAsyncDisposable
        {
            internal IHost Host => host;
            internal TemporaryDirectory Directory => directory;
            internal int Port => port;
            internal Uri Endpoint => new($"http://127.0.0.1:{port}/mcp");
            internal IMcpHostControl Control => host.Services.GetRequiredService<IMcpHostControl>();
            internal IMcpStatusReader Status => host.Services.GetRequiredService<IMcpStatusReader>();
            internal async Task<long> ScalarAsync(string sql)
            {
                await using var connection = host.Services.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
                await connection.OpenAsync(Token);
                return await connection.ExecuteScalarAsync<long>(sql);
            }
            public async ValueTask DisposeAsync()
            {
                await host.StopAsync(CancellationToken.None);
                if (host is IAsyncDisposable disposable) await disposable.DisposeAsync(); else host.Dispose();
                directory.Dispose();
            }
        }
    }
}
