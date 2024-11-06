using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections;

namespace Boris.BinaryExtraction.Azure
{
    public class SanityCheckService
    {
        private readonly string _databaseName;
        private readonly ILogger<SanityCheckService> _logger;
        private readonly DataRepository _dataRepository;
        private AzureStorageProviderService _azureStorageProviderService;

        private readonly int _attachmentExternalBinaryProviderId;
        private readonly int _genericAttachmentExternalBinaryProviderId;
        private readonly int _woAttachmentExternalBinaryProviderId;

        public SanityCheckService(ILogger<SanityCheckService> logger, IConfiguration configuration, DataRepository dataRepository, AzureStorageProviderService azureStorageProviderService, string databaseName) {
           _databaseName = databaseName;
            _logger = logger;
            _dataRepository = dataRepository;
            _azureStorageProviderService = azureStorageProviderService;

            _attachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:Attachments:ExternalBinaryProviderId"], out var externalIdAtt) ? externalIdAtt : 0;
            _genericAttachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:ExternalBinaryProviderId"], out var externalIdGA) ? externalIdGA : 0;
            _woAttachmentExternalBinaryProviderId = int.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:ExternalBinaryProviderId"], out var externalIdWOA) ? externalIdWOA : 0;
        }

        internal async Task SanityCheckAttachments(string connectionString)
        {
            _logger.LogInformation($" Method : SanityCheckAttachments, Database : {_databaseName}, Table: tblAttachments, Desc: Sanity check started!. ");
            try
            {
                var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _attachmentExternalBinaryProviderId);
                if (azureBSConfig != null)
                {
                    int blobCount = 0;
                    int remainingCount = await _dataRepository.GetRemainingAttachmentsEntriesCount(connectionString);
                    string storageConnectionString = await _azureStorageProviderService.GetConnectionString(azureBSConfig.AccountName, azureBSConfig.AccountKey);
                    // Create a CloudBlobClient object for accessing the Blob Storage service
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = blobClient.GetContainerReference(azureBSConfig.ContainerName);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    // Get all attachments Ids with external binary Id 100 and moved to Azure
                    var idList = await _dataRepository.GetAllExternalAttachmentsEntries(connectionString, _attachmentExternalBinaryProviderId);
                    foreach (var id in idList)
                    {
                        string blobName = $"{azureBSConfig.ExternalPath}/{id}";
                        CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                        bool isBlobExists = await blob.ExistsAsync();
                        if (isBlobExists)
                        {
                            blobCount++;
                        }
                        else
                        {
                            _logger.LogError($"Method: SanityCheckAttachments, Database: {_databaseName}, Table: tblAttachments, Desc: Id: {id} not found in the Container: {azureBSConfig.ContainerName}, ExternalPath: {azureBSConfig.ExternalPath}, BlobName: {blobName}");
                        }
                    }
                    _logger.LogInformation($"Method : SanityCheckAttachments, Database: {_databaseName}, Table: tblAttachments, Desc: Sanity check results; SQL Attachment Count:{idList.Count}, Blob Count:{blobCount}, Remaining Attachment Count:{remainingCount}");
                    if (idList.Count != blobCount)
                    {
                        _logger.LogWarning($"Method: SanityCheckAttachments, Database: {_databaseName}, Table: tblAttachments, Desc: Not all moved attachments are found in blob storage. Need immediate ATTENTION!");
                    }
                }
                else
                {
                    _logger.LogInformation($"Method : SanityCheckAttachments, Database: {_databaseName}, Table: tblAttachments, Desc: Sanity check results; SQL Attachment Count: -1, Blob Count: -1, Remaining Attachment Count: -1");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method : SanityCheckAttachments, Database: {_databaseName}, Table: tblAttachments, Desc: Sanity check failed!, {ex}");
                throw;
            }
        }

        internal async Task SanityCheckGenericAttachments(string connectionString)
        {
            _logger.LogInformation($" Method : SanityCheckGenericAttachments, Database : {_databaseName}, Table: tblGenericAttachments, Desc: Sanity check started!. ");

            try
            {
                var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _genericAttachmentExternalBinaryProviderId);
                if (azureBSConfig!=null)
                {
                    int blobCount = 0;
                    int remainingCount = await _dataRepository.GetRemainingGenericAttachmentsEntriesCount(connectionString);

                    string storageConnectionString = await _azureStorageProviderService.GetConnectionString(azureBSConfig.AccountName, azureBSConfig.AccountKey);
                    // Create a CloudBlobClient object for accessing the Blob Storage service
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = blobClient.GetContainerReference(azureBSConfig.ContainerName);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    // Get all generic attachments Ids with external binary Id 101 and moved to Azure
                    var idList = await _dataRepository.GetAllExternalGenericAttachmentsEntries(connectionString, _genericAttachmentExternalBinaryProviderId);
                    foreach (var id in idList)
                    {
                        string blobName = $"{azureBSConfig.ExternalPath}/{id}";
                        CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                        bool isBlobExists = await blob.ExistsAsync();
                        if (isBlobExists)
                        {
                            blobCount++;
                        }
                        else
                        {
                            _logger.LogError($"Method: SanityCheckGenericAttachments, Database: {_databaseName}, Table: tblGenericAttachments, Desc: Id: {id} not found in the Container: {azureBSConfig.ContainerName}, ExternalPath: {azureBSConfig.ExternalPath}, BlobName: {blobName}");
                        }
                    }
                    _logger.LogInformation($"Method : SanityCheckGenericAttachments, Database: {_databaseName}, Table: tblGenericAttachments, Desc: Sanity check results; SQL Attachment Count:{idList.Count}, Blob Count:{blobCount},  Remaining Attachment Count:{remainingCount}");
                    if (idList.Count!= blobCount)
                    {
                        _logger.LogWarning($"Method: SanityCheckGenericAttachments, Database: {_databaseName}, Table: tblGenericAttachments, Desc: Not all moved attachments are found in blob storage. Need immediate ATTENTION!");
                    }
                }
                else
                {
                    _logger.LogInformation($"Method : SanityCheckGenericAttachments, Database: {_databaseName}, Table: tblGenericAttachments, Desc: Sanity check results; SQL Attachment Count: -1, Blob Count: -1,  Remaining Attachment Count: -1");
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Method : SanityCheckGenericAttachments, Database: {_databaseName}, Table: tblGenericAttachments, Desc: Sanity check failed!, {ex}");
                throw;
            }
        }

        internal async Task SanityCheckWorkOrderAttachments(string connectionString)   
        {
            _logger.LogInformation($" Method : SanityCheckWorkOrderAttachments, Database : {_databaseName}, Table: tblWorkOrderAttachments, Desc: Sanity check started!. ");

            try
            {
                var azureBSConfig = await _azureStorageProviderService.GetAzureBSConfigs(connectionString, _databaseName, _woAttachmentExternalBinaryProviderId);
                if (azureBSConfig != null)
                {
                    int blobCount = 0;
                    int remainingCount = await _dataRepository.GetRemainingWorkOrderAttachmentsEntriesCount(connectionString);

                    string storageConnectionString = await _azureStorageProviderService.GetConnectionString(azureBSConfig.AccountName, azureBSConfig.AccountKey);
                    // Create a CloudBlobClient object for accessing the Blob Storage service
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = blobClient.GetContainerReference(azureBSConfig.ContainerName);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    // Get all work order attachments Ids with external binary Id 102 and moved to Azure
                    var idList = await _dataRepository.GetAllExternalWorkOrderAttachmentsEntries(connectionString, _woAttachmentExternalBinaryProviderId);
                    foreach (var id in idList)
                    {
                        string blobName = $"{azureBSConfig.ExternalPath}/{id}";
                        CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                        bool isBlobExists = await blob.ExistsAsync();
                        if (isBlobExists)
                        {
                            blobCount++;
                        }
                        else
                        {
                            _logger.LogError($"Method: SanityCheckWorkOrderAttachments, Database: {_databaseName}, Table: tblWorkOrderAttachments, Desc: Id: {id} not found in the Container: {azureBSConfig.ContainerName}, ExternalPath: {azureBSConfig.ExternalPath}, BlobName: {blobName}");
                        }
                    }
                    _logger.LogInformation($"Method : SanityCheckWorkOrderAttachments, Database: {_databaseName}, Table: tblWorkOrderAttachments, Desc: Sanity check results; SQL Attachment Count:{idList.Count}, Blob Count:{blobCount},  Remaining Attachment Count:{remainingCount}");
                    if (idList.Count != blobCount)
                    {
                        _logger.LogWarning($"Method: SanityCheckWorkOrderAttachments, Database: {_databaseName}, Table: tblWorkOrderAttachments, Desc: Not all moved attachments are found in blob storage. Need immediate ATTENTION!");
                    }
                }
                else
                {
                    _logger.LogInformation($"Method : SanityCheckWorkOrderAttachments, Database: {_databaseName}, Table: tblWorkOrderAttachments, Desc: Sanity check results; SQL Attachment Count: -1, Blob Count: -1,  Remaining Attachment Count: -1");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method : SanityCheckWorkOrderAttachments, Database: {_databaseName}, Table: tblWorkOrderAttachments, Desc: Sanity check failed!, {ex}");
                throw;
            }

        }
    }
}
