using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public interface IDatabaseServiceFactory
    {
        T CreateService<T>(string databaseName) where T : class;
    }

    public class DatabaseServiceFactory : IDatabaseServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseLoggerProvider _loggerProvider;
        private readonly DataRepository _dataRepository;
        private readonly AzureStorageProviderService _storageProviderService;

        public DatabaseServiceFactory(IConfiguration configuration, DatabaseLoggerProvider loggerProvider, DataRepository dataRepository, AzureStorageProviderService storageProviderService)
        {
            _configuration = configuration;
            _loggerProvider = loggerProvider;
            _dataRepository = dataRepository;
            _storageProviderService = storageProviderService;
        }

        public T CreateService<T>(string databaseName) where T : class
        {
            var logger = _loggerProvider.GetDatabaseLogger<T>(databaseName);

            if (typeof(T) == typeof(SanityCheckService))
            {
                return new SanityCheckService((ILogger<SanityCheckService>)logger, _configuration, _dataRepository, _storageProviderService, databaseName) as T;
            }
            else if (typeof(T) == typeof(MultiThreadingService))
            {
                return new MultiThreadingService((ILogger<MultiThreadingService>)logger, _configuration, _dataRepository, _storageProviderService, databaseName) as T;
            }

            throw new InvalidOperationException($"Service type {typeof(T).Name} is not supported by the factory.");
        }
    }

}
