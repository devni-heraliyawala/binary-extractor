using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Boris.BinaryExtraction.Azure
{
    public class AzureStorageProviderService
    {
        private readonly ILogger<AzureStorageProviderService> _logger;
        private readonly DataRepository _dataRepository;
        public AzureStorageProviderService(ILogger<AzureStorageProviderService> logger, DataRepository dataRepository)
        {
            _logger = logger;
            _dataRepository = dataRepository;
        }

        public async Task<bool> UploadToAzure(string databaseName, string blobName, byte[] fileContent, AzureBSConfig azureBSConfig)
        {
            _logger.LogInformation($"Upload to Azure Started for BlobName : {blobName}, ExternalPath : {azureBSConfig.ExternalPath}, Container Name : {azureBSConfig.ContainerName}");
            bool isSuccess = false;
            var currentBlobName = blobName;

            // Prevent overwriting an existing blob
            AccessCondition accessCondition = AccessCondition.GenerateIfNoneMatchCondition("*");

            // Set blob upload options and retry policies
            BlobRequestOptions options = new BlobRequestOptions()
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(3), 3)  // Built-in retry policy
            };

            // Operation context for tracking
            OperationContext operationContext = new OperationContext();
            operationContext.RequestCompleted += (sender, args) =>
            {
                _logger.LogError($"Method: UploadToAzure| Entity: Azure | Database: {databaseName} | Container Name :{azureBSConfig.ContainerName} | Retrying operation for Blob: {currentBlobName}. Error: {args.RequestInformation.HttpStatusMessage}");
            };
            try
            {
                blobName = $"{azureBSConfig.ExternalPath}/{blobName}";

                string containerName = azureBSConfig.ContainerName;
                string connectionString = await GetConnectionString(azureBSConfig.AccountName, azureBSConfig.AccountKey);

                // Create a CloudStorageAccount object using the connection string
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

                // Create a CloudBlobClient object from the storage account
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // Get a reference to the container
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Create the container if it doesn't exist
                await container.CreateIfNotExistsAsync();

                // Get a reference to the blob
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

                if (fileContent != null)
                {
                    // Upload the byte array to the blob
                    using (Stream stream = new MemoryStream(fileContent))
                    {
                        try
                        {
                            // First attempt without retry policy
                            await blockBlob.UploadFromStreamAsync(stream, accessCondition, null, null);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Method: UploadToAzure| Entity: Azure | Database: {databaseName} | Container Name :{azureBSConfig.ContainerName} | Desc: Initial upload attempt failed for Blob: {blobName}. Retrying with retry policy. Error: {ex.Message}");

                            // Retry with the configured retry policy on failure
                            stream.Position = 0; // Reset the stream position
                            blockBlob.UploadFromStreamAsync(stream, accessCondition, options, operationContext).GetAwaiter().GetResult();
                        }

                    }
                   isSuccess = await blockBlob.ExistsAsync();
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                _logger.LogError($"Method: UploadToAzure| Entity: Azure | Database: {databaseName} | Container Name :{azureBSConfig.ContainerName} | ExternalPath : {azureBSConfig.ExternalPath}| BlobName : {blobName} | Message: {ex.Message}| StackTrace:{ex.StackTrace}");

            }


            return isSuccess;
        }

        public async Task<string> GetConnectionString(string accountName, string accountKey)
        {
            return $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
        }

        public async Task<AzureBSConfig?> GetAzureBSConfigs(string connectionString, string databaseName, int externalBinaryProviderId)
        {
            try
            {
                bool isValidConfigs = true;
                var externalConfigs = await _dataRepository.GetExternalBinaryConfig(connectionString, externalBinaryProviderId);
                if (externalConfigs!= null)
                {
                    var settingsElement = XElement.Parse(externalConfigs.XmlDocument);

                    // Find the ExternalBinaryProvider element
                    XElement externalBinaryProviderElement = settingsElement.Element("ExternalBinaryProvider");

                    // Read the accountName attribute
                    string accountKey = externalBinaryProviderElement?.Attribute("accountKey")?.Value ?? string.Empty;
                    string accountName = externalBinaryProviderElement?.Attribute("accountName")?.Value ?? string.Empty;
                    string containerName = externalBinaryProviderElement?.Attribute("containerName")?.Value ?? string.Empty;
                    string externalPath = externalBinaryProviderElement?.Attribute("externalPath")?.Value ?? string.Empty;

                    if (string.IsNullOrEmpty(accountKey))
                    {
                        isValidConfigs = false;
                        _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | Account Key not found in XML.| ExternalBinaryProviderId: {externalBinaryProviderId}");
                    }
                    if (string.IsNullOrEmpty(accountName))
                    {
                        isValidConfigs = false;
                        _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | Account Name not found in XML.| ExternalBinaryProviderId: {externalBinaryProviderId}");
                    }

                    if (string.IsNullOrEmpty(containerName))
                    {
                        isValidConfigs = false;
                        _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | Container Name not found in XML.| ExternalBinaryProviderId: {externalBinaryProviderId}");
                    }

                    if (string.IsNullOrEmpty(externalPath))
                    {
                        isValidConfigs = false;
                        _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | External Path not found in XML.| ExternalBinaryProviderId: {externalBinaryProviderId}");
                    }

                    if (!string.IsNullOrEmpty(externalPath) && externalPath != externalConfigs.RootDirectoryPath)
                    {
                        isValidConfigs = false;
                        _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | External Path found in XML Field and RootDirectoryPath are mismatched.| ExternalBinaryProviderId: {externalBinaryProviderId}");
                    }

                    if (isValidConfigs)
                    {
                        var config = new AzureBSConfig()
                        {
                            AccountKey = accountKey,
                            AccountName = accountName,
                            ContainerName = containerName,
                            ExternalPath = externalPath
                        };
                        return (config);
                    }
                }
                else
                {
                    _logger.LogError($"Method: GetAzureBSConfigs | Database:{databaseName} | Desc: Azure blog storage configs not found! |ExternalBinaryProviderId: {externalBinaryProviderId}");
                }
                return (null);
            }
            catch (Exception ex)
            {
                throw;
            }
           
        }
    }
}
