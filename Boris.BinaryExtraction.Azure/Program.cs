using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Boris.BinaryExtraction.Azure;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog as the logging provider using the configuration from the appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)  // This reads configuration settings from appsettings.json
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day,shared:true, outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")  // Log to file with daily rolling
    .CreateLogger();

// Clear default logging providers and add Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddSingleton<DataContextFactory>();
builder.Services.AddTransient<DataRepository>();
builder.Services.AddSingleton<DatabaseLoggerProvider>();
builder.Services.AddSingleton<IDatabaseServiceFactory, DatabaseServiceFactory>();
builder.Services.AddTransient<AzureStorageProviderService>();
// Configure to run as a Windows Service
builder.Services.AddHostedService<Worker>();

try
{
    Log.Information("Starting up the service...");
    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly");
}
finally
{
    Log.CloseAndFlush();
}
