using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public class MultiThreadingService
    {
        private readonly string _databaseName;
        private readonly ILogger<MultiThreadingService> _logger;
        private readonly DataRepository _dataRepository;
        private AzureStorageProviderService _azureStorageProviderService;

        private readonly bool _enableAttachmentMigration;
        private readonly bool _enableGenericAttachmentMigration;
        private readonly bool _enableWOAttachmentMigration;

        private readonly int _attachmentExternalBinaryProviderId;
        private readonly int _genericAttachmentExternalBinaryProviderId;
        private readonly int _woAttachmentExternalBinaryProviderId;

        private readonly int _attachmentConcurrentCount;
        private readonly int _genericAttachmentConcurrentCount;
        private readonly int _woAttachmentConcurrentCount;

        private readonly int _attachmentChunkSize;
        private readonly int _genericAttachmentChunkSize;
        private readonly int _woAttachmentChunkSize;

        public MultiThreadingService(ILogger<MultiThreadingService> logger, IConfiguration configuration, DataRepository dataRepository, AzureStorageProviderService azureStorageProviderService, string databaseName)
        {
            _databaseName = databaseName;
            _dataRepository = dataRepository;
            _azureStorageProviderService = azureStorageProviderService;
            _logger = logger; // Assign to non-generic field if preferred

            _enableAttachmentMigration = bool.TryParse(configuration["AppSettings:AzureStorage:Attachments:EnableForMigration"], out var enableAtt) ? enableAtt : false;
            _enableGenericAttachmentMigration = bool.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:EnableForMigration"], out var enableGA) ? enableGA : false;
            _enableWOAttachmentMigration = bool.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:EnableForMigration"], out var enableWOA) ? enableWOA : false;


            _attachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:Attachments:ExternalBinaryProviderId"], out var externalIdAtt) ? externalIdAtt : 0;
            _genericAttachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:ExternalBinaryProviderId"], out var externalIdGA) ? externalIdGA : 0;
            _woAttachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:ExternalBinaryProviderId"], out var externalIdWOA) ? externalIdWOA : 0;

            _attachmentConcurrentCount = int.TryParse(configuration["AppSettings:AzureStorage:Attachments:ConcurrentCount"], out var ccountAtt) ? ccountAtt : 0;
            _genericAttachmentConcurrentCount = int.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:ConcurrentCount"], out var ccountGA) ? ccountGA : 0;
            _woAttachmentConcurrentCount = int.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:ConcurrentCount"], out var ccountWOA) ? ccountWOA : 0;

            _attachmentChunkSize = int.TryParse(configuration["AppSettings:AzureStorage:Attachments:ChunkSize"], out var chunkSizeAtt) ? chunkSizeAtt : 0;
            _genericAttachmentChunkSize = int.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:ChunkSize"], out var chunkSizeGA) ? chunkSizeGA : 0;
            _woAttachmentChunkSize = int.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:ChunkSize"], out var chunkSizeWOA) ? chunkSizeWOA : 0;
            _logger = logger;
        }

        public async Task MoveAttachmentData(string connectionString)
        {
            try
            {
                _logger.LogInformation($"Starting MoveAttachmentData process. Database: {_databaseName}");

                if (_enableAttachmentMigration && _attachmentExternalBinaryProviderId != 0)
                {
                    var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _attachmentExternalBinaryProviderId);
                    if (azureBSConfig != null)
                    {
                        SemaphoreSlim throttler = new SemaphoreSlim(_attachmentConcurrentCount);  // Adjust the concurrency level
                        List<Task> tasks = new List<Task>();

                        int totalCount = await _dataRepository.GetTotalAttachmentsEntriesCount(connectionString);
                        int remainingCount = await _dataRepository.GetRemainingAttachmentsEntriesCount(connectionString);
                        while (remainingCount > 0)
                        {
                            // Get the top 200 attachment Ids
                            var IdList = await _dataRepository.GetTopKRemainingAttachmentsEntries(connectionString);
                            // List to hold the sublists
                            List<List<int>> chunkedLists = new List<List<int>>();

                            // Size of each chunk
                            int chunkSize = _attachmentChunkSize;

                            // Creating sublists
                            for (int i = 0; i < IdList.Count; i += chunkSize)
                            {
                                List<int> chunk = IdList.GetRange(i, Math.Min(chunkSize, IdList.Count - i));
                                await throttler.WaitAsync();
                                tasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        var attachmentModels = await _dataRepository.GetAttachmentsDataRange(connectionString, _databaseName, chunk);

                                        //Move data to azure
                                        foreach (var attachmentModel in attachmentModels)
                                        {
                                            int attachmentId = attachmentModel.Id;
                                            byte[] attachmentData = attachmentModel.AttachmentData;

                                            bool isUploadSuccess = await _azureStorageProviderService.UploadToAzure(_databaseName, attachmentId.ToString(), attachmentData, azureBSConfig);
                                            if (isUploadSuccess)
                                            {
                                                _logger.LogInformation($"Method: MoveAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblAttachments | Id : {attachmentId} | Desc: Azure upload success!");
                                                bool isDbUpdateSuccess = await _dataRepository.UpdateAttachments(connectionString, _attachmentExternalBinaryProviderId, attachmentId);
                                                if (isDbUpdateSuccess)
                                                {
                                                    _logger.LogInformation($"Method: MoveAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblAttachments | Id : {attachmentId} | Desc: SQL update success!");
                                                }
                                                else
                                                {
                                                    _logger.LogError($"Method: MoveAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblAttachments | Id : {attachmentId} | Desc: SQL update failed!");
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogError($"Method: MoveAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblAttachments | Id : {attachmentId} | Desc: Azure upload failed!.");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        throttler.Release();
                                    }
                                }));
                            }
                            // Wait for all tasks to complete
                            await Task.WhenAll(tasks);

                            // Update remaining count after tasks completion
                            remainingCount = remainingCount - IdList.Count;
                            _logger.LogInformation($"Method: MoveAttachmentData | Database : {_databaseName} | Table: tblAttachments | Desc: Remaining entries count (IN-PROGRESS): {remainingCount}");
                            _logger.LogInformation($"Method: MoveAttachmentData | Database : {_databaseName} | Table: tblAttachments | Desc: Binary extractions progress (IN-PROGRESS): {((double)(totalCount - remainingCount) / totalCount) * 100} % completed");
                        }

                        int remainingCountActual = await _dataRepository.GetRemainingAttachmentsEntriesCount(connectionString);
                        _logger.LogInformation($"Method: MoveAttachmentData | Database : {_databaseName} | Table: tblAttachments | Desc: Remaining entries count (ACTUAL): {remainingCountActual}");
                        _logger.LogInformation($"Method: MoveAttachmentData | Database : {_databaseName} | Table: tblAttachments | Desc: Binary extractions progress (ACTUAL): {((double)(totalCount - remainingCountActual) / totalCount) * 100} % completed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in MoveAttachmentData. Database: {_databaseName}");
            }
        }

        public async Task MoveGenericAttachmentData(string connectionString)
        {
            try
            {
                _logger.LogInformation($"Starting MoveGenericAttachmentData process. Database: {_databaseName}");

                if (_enableGenericAttachmentMigration && _genericAttachmentExternalBinaryProviderId != 0)
                {
                    var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _genericAttachmentExternalBinaryProviderId);
                    if (azureBSConfig != null)
                    {
                        SemaphoreSlim throttler = new SemaphoreSlim(_genericAttachmentConcurrentCount);  // Adjust the concurrency level
                        List<Task> tasks = new List<Task>();

                        int totalCount = await _dataRepository.GetTotalGenericAttachmentsEntriesCount(connectionString);
                        int remainingCount = await _dataRepository.GetRemainingGenericAttachmentsEntriesCount(connectionString);
                        while (remainingCount > 0)
                        {
                            // Get the top 200 attachment Ids
                            var IdList = await _dataRepository.GetTopKRemainingGenericAttachmentsEntries(connectionString);
                            // List to hold the sublists
                            List<List<int>> chunkedLists = new List<List<int>>();

                            // Size of each chunk
                            int chunkSize = _genericAttachmentChunkSize;

                            // Creating sublists
                            for (int i = 0; i < IdList.Count; i += chunkSize)
                            {
                                List<int> chunk = IdList.GetRange(i, Math.Min(chunkSize, IdList.Count - i));
                                await throttler.WaitAsync();
                                tasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        var attachmentModels = await _dataRepository.GetGenericAttachmentsDataRange(connectionString, _databaseName, chunk);

                                        //Move data to azure
                                        foreach (var attachmentModel in attachmentModels)
                                        {
                                            int attachmentId = attachmentModel.Id;
                                            byte[] attachmentData = attachmentModel.AttachmentData;

                                            bool isUploadSuccess = await _azureStorageProviderService.UploadToAzure(_databaseName, attachmentId.ToString(), attachmentData, azureBSConfig);
                                            if (isUploadSuccess)
                                            {
                                                _logger.LogInformation($"Method: MoveGenericAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblGenericAttachments | Id : {attachmentId} | Desc: Azure upload success!");

                                                bool isDbUpdateSuccess = await _dataRepository.UpdateGenericAttachments(connectionString, _genericAttachmentExternalBinaryProviderId, attachmentId);
                                                if (isDbUpdateSuccess)
                                                {
                                                    _logger.LogInformation($"Method: MoveGenericAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblGenericAttachments | Id : {attachmentId} | Desc: SQL update success!");
                                                }
                                                else
                                                {
                                                    _logger.LogError($"Method: MoveGenericAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} |  Table: tblGenericAttachments | Id : {attachmentId} | Desc: SQL update failed!");
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogError($"Method: MoveGenericAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblGenericAttachments | Id : {attachmentId} | Desc: Azure upload failed!.");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        throttler.Release();
                                    }
                                }));
                            }
                            // Wait for all tasks to complete
                            await Task.WhenAll(tasks);

                            // Update remaining count after tasks completion
                            remainingCount = remainingCount - IdList.Count;
                            _logger.LogInformation($"Method: MoveGenericAttachmentData | Database : {_databaseName} | Table: tblGenericAttachments | Desc: Remaining entries count (IN-PROGRESS): {remainingCount}");
                            _logger.LogInformation($"Method: MoveGenericAttachmentData | Database : {_databaseName} | Table: tblGenericAttachments | Desc: Binary extractions progress (IN-PROGRESS): {((double)(totalCount - remainingCount) / totalCount) * 100} % completed");
                        }

                        int remainingCountActual = await _dataRepository.GetRemainingGenericAttachmentsEntriesCount(connectionString);
                        _logger.LogInformation($"Method: MoveGenericAttachmentData | Database : {_databaseName} | Table: tblGenericAttachments | Desc: Remaining entries count (ACTUAL): {remainingCountActual}");
                        _logger.LogInformation($"Method: MoveGenericAttachmentData | Database : {_databaseName} | Table: tblGenericAttachments | Desc: Binary extractions progress (ACTUAL): {((double)(totalCount - remainingCountActual) / totalCount) * 100} % completed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in MoveGenericAttachmentData. Database: {_databaseName}");
            }
        }

        public async Task MoveWorkOrderAttachmentData(string connectionString)
        {
            try
            {
                _logger.LogInformation($"Starting MoveWorkOrderAttachmentData process. Database: {_databaseName}");

                if (_enableWOAttachmentMigration && _woAttachmentExternalBinaryProviderId != 0)
                {
                    var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _woAttachmentExternalBinaryProviderId);
                    if (azureBSConfig != null)
                    {

                        SemaphoreSlim throttler = new SemaphoreSlim(_woAttachmentConcurrentCount);  // Adjust the concurrency level
                        List<Task> tasks = new List<Task>();

                        int totalCount = await _dataRepository.GetTotalWorkOrderAttachmentsEntriesCount(connectionString);
                        int remainingCount = await _dataRepository.GetRemainingWorkOrderAttachmentsEntriesCount(connectionString);
                        while (remainingCount > 0)
                        {
                            // Get the top 200 attachment Ids
                            var IdList = await _dataRepository.GetTopKRemainingWorkOrderAttachmentsEntries(connectionString);
                            // List to hold the sublists
                            List<List<int>> chunkedLists = new List<List<int>>();

                            // Size of each chunk
                            int chunkSize = _woAttachmentChunkSize;

                            // Creating sublists
                            for (int i = 0; i < IdList.Count; i += chunkSize)
                            {
                                List<int> chunk = IdList.GetRange(i, Math.Min(chunkSize, IdList.Count - i));
                                await throttler.WaitAsync();
                                tasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        var attachmentModels = await _dataRepository.GetWorkOrderAttachmentsDataRange(connectionString, _databaseName, chunk);

                                        //Move data to azure
                                        foreach (var attachmentModel in attachmentModels)
                                        {
                                            int attachmentId = attachmentModel.Id;
                                            byte[] attachmentData = attachmentModel.AttachmentData;

                                            bool isUploadSuccess = await _azureStorageProviderService.UploadToAzure(_databaseName, attachmentId.ToString(), attachmentData, azureBSConfig);
                                            if (isUploadSuccess)
                                            {
                                                _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblWorkOrderAttachments | Id : {attachmentId} | Desc: Azure upload success!");

                                                bool isDbUpdateSuccess = await _dataRepository.UpdateWorkOrderAttachments(connectionString, _woAttachmentExternalBinaryProviderId, attachmentId);
                                                if (isDbUpdateSuccess)
                                                {
                                                    _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblWorkOrderAttachments | Id : {attachmentId} | Desc: SQL update success!");
                                                }
                                                else
                                                {
                                                    _logger.LogError($"Method: MoveWorkOrderAttachmentData | Entity: SQL | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} |  Table: tblWorkOrderAttachments | Id : {attachmentId} | Desc: SQL update failed!");
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogError($"Method: MoveWorkOrderAttachmentData | Entity: Azure | Database: {_databaseName} | Container Name: {azureBSConfig.ContainerName} | Table: tblWorkOrderAttachments | Id : {attachmentId} | Desc: Azure upload failed!.");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        throttler.Release();
                                    }
                                }));
                            }
                            // Wait for all tasks to complete
                            await Task.WhenAll(tasks);

                            // Update remaining count after tasks completion
                            remainingCount = remainingCount - IdList.Count;
                            _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Database : {_databaseName} | Table: tblWorkOrderAttachments | Desc: Remaining entries count (IN-PROGRESS): {remainingCount}");
                            _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Database : {_databaseName} | Table: tblWorkOrderAttachments | Desc: Binary extractions progress (IN-PROGRESS): {((double)(totalCount - remainingCount) / totalCount) * 100} % completed");
                        }

                        int remainingCountActual =await _dataRepository.GetRemainingWorkOrderAttachmentsEntriesCount(connectionString);
                        _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Database : {_databaseName} | Table: tblWorkOrderAttachments | Desc: Remaining entries count (ACTUAL): {remainingCountActual}");
                        _logger.LogInformation($"Method: MoveWorkOrderAttachmentData | Database : {_databaseName} | Table: tblWorkOrderAttachments | Desc: Binary extractions progress (ACTUAL): {((double)(totalCount - remainingCountActual) / totalCount) * 100} % completed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in MoveWorkOrderAttachmentData. Database: {_databaseName}");
            }
        }
    }
}
