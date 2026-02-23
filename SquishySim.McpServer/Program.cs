// PROTOTYPE: SquishySim MCP server — thin proxy to SquishySim.Api REST endpoints
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SquishySim.McpServer;

var apiBaseUrl = Environment.GetEnvironmentVariable("SQUISHYSIM_API_URL") ?? "http://localhost:5300";

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddHttpClient<SimTools>(client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
