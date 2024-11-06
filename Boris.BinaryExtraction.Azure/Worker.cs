using Microsoft.Extensions.Configuration;
using Serilog.Context;
using System.Data.SqlClient;
using System.Diagnostics;

namespace Boris.BinaryExtraction.Azure
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IDatabaseServiceFactory _databaseServiceFactory;
        private readonly DatabaseLoggerProvider _databaseLoggerProvider;

        public Worker(IConfiguration configuration, IDatabaseServiceFactory databaseServiceFactory, DatabaseLoggerProvider databaseLoggerProvider)
        {
            _configuration = configuration;
            _databaseServiceFactory = databaseServiceFactory;
            _databaseLoggerProvider = databaseLoggerProvider;
        }
        string FormatDuration(TimeSpan duration) => $"{(int)duration.TotalHours}h: {duration.Minutes}m: {duration.Seconds}s: {duration.Milliseconds}ms";


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connectionStrings = _configuration.GetSection("ConnectionStrings").Get<Dictionary<string, string>>();

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var connectionString in connectionStrings.Values)
                {
                    var builder = new SqlConnectionStringBuilder(connectionString);
                    string databaseName = builder.InitialCatalog;

                    // Create instances of the services with database-specific loggers
                    var sanityCheckService = _databaseServiceFactory.CreateService<SanityCheckService>(databaseName);
                    var multiThreadingService = _databaseServiceFactory.CreateService<MultiThreadingService>(databaseName);


                    // Log using LogContext to set the DatabaseName context property
                    using (LogContext.PushProperty("DatabaseName", databaseName))
                    {
                        // Obtain a database-specific logger
                        var logger = _databaseLoggerProvider.GetDatabaseLogger(databaseName);

                        logger.LogInformation($"---------------------------------------------- Executing Time Summary for {databaseName} ----------------------------------------------");

                        var overallStopwatch = Stopwatch.StartNew();

                        // Track and log times for each operation
                        await TrackAndLogOperationAsync(
                            async () => await multiThreadingService.MoveAttachmentData(connectionString),
                            "MoveAttachmentData",
                            databaseName,
                            logger
                        );

                        await TrackAndLogOperationAsync(
                            async () => await multiThreadingService.MoveGenericAttachmentData(connectionString),
                            "MoveGenericAttachmentData",
                            databaseName,
                            logger
                        );

                        await TrackAndLogOperationAsync(
                            async () => await multiThreadingService.MoveWorkOrderAttachmentData(connectionString),
                            "MoveWorkOrderAttachmentData",
                            databaseName,
                            logger
                        );

                        await TrackAndLogOperationAsync(
                            async () => await sanityCheckService.SanityCheckAttachments(connectionString),
                            "SanityCheckAttachments",
                            databaseName,
                            logger
                        );

                        await TrackAndLogOperationAsync(
                            async () => await sanityCheckService.SanityCheckGenericAttachments(connectionString),
                            "SanityCheckGenericAttachments",
                            databaseName,
                            logger
                        );

                        await TrackAndLogOperationAsync(
                            async () => await sanityCheckService.SanityCheckWorkOrderAttachments(connectionString),
                            "SanityCheckWorkOrderAttachments",
                            databaseName,
                            logger
                        );

                        overallStopwatch.Stop();
                        logger.LogInformation("Total duration for processing database {databaseName}: {totalDuration}", databaseName, FormatDuration(overallStopwatch.Elapsed));
                        logger.LogInformation($"---------------------------------------------- End of Summary for {databaseName} ------------------------------------------------");
                    }

                    // Optional delay between processing each database
                    await Task.Delay(1000, stoppingToken);
                }

                // Delay to avoid restarting the loop immediately
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task TrackAndLogOperationAsync(Func<Task> operation, string operationName, string databaseName, ILogger logger)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();

            logger.LogInformation("Duration of {operationName} for database {databaseName}: {duration}",
                operationName, databaseName, FormatDuration(stopwatch.Elapsed));
        }
    }
}
