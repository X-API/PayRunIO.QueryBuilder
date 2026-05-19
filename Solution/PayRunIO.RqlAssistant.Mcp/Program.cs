using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PayRunIO.RqlAssistant.Service;

var builder = Host.CreateApplicationBuilder(args);

// stdio MCP transport uses stdout for protocol frames — route all logs to stderr to avoid corrupting it.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<IDocumentRepository, DocumentRepository>();
builder.Services.AddSingleton<IQueryValidator, QueryValidator>();
builder.Services.AddSingleton<IRqlGrammarIndex, RqlGrammarIndex>();
builder.Services.AddSingleton<IRqlToolDispatcher, RqlToolDispatcher>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
