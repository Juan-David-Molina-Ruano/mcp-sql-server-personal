using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpSqlServerPersonal;
using McpSqlServerPersonal.Security;

// Load and validate configuration from environment variables before starting the host.
var (config, errors) = ConfigValidator.LoadAndValidate();
if (config is null)
{
    foreach (string error in errors)
    {
        Console.Error.WriteLine($"[Config Error] {error}");
    }
    Environment.Exit(1);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Ensure all logging goes to stderr so stdout is reserved for MCP protocol messages.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register configuration so tools can resolve it via DI.
builder.Services.AddSingleton(config);

// Register SQL connection factory for database access.
builder.Services.AddSingleton<SqlConnectionFactory>();

// Register security layer for sensitive data obfuscation.
// Use a factory to avoid DI injecting an empty IEnumerable<string> for the optional constructor parameter.
builder.Services.AddSingleton(_ => new SensitiveColumnPolicy());
builder.Services.AddSingleton<DataObfuscator>();
builder.Services.AddSingleton<ResultSetSanitizer>();

// Register MCP server with stdio transport and discover tools from this assembly.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
