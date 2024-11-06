using Serilog.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Boris.BinaryExtraction.Azure
{
    public class DatabaseLoggerProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public DatabaseLoggerProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }
        public ILogger GetDatabaseLogger(string databaseName)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.File(
                    path: $"logs/{databaseName}/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            return _loggerFactory.AddSerilog(logger).CreateLogger(databaseName);
        }
        public ILogger<T> GetDatabaseLogger<T>(string databaseName)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.File(
                    path: $"logs/{databaseName}/log-.txt", // Each database gets its own folder
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            return _loggerFactory.AddSerilog(logger).CreateLogger<T>();
        }
    }

}