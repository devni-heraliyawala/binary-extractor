using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public class DataRepository
    {
        private readonly DataContextFactory _contextFactory;
        private readonly ILogger<DataRepository> _logger;

        private readonly int _defaultBatchSize;
        private readonly int _attachmentBatchSize;
        private readonly int _genericAttachmentBatchSize;
        private readonly int _woAttachmentBatchSize;

        public DataRepository(DataContextFactory contextFactory, ILogger<DataRepository> logger, IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _logger = logger;

            _defaultBatchSize = int.TryParse(configuration["AppSettings:AzureStorage:DefaultBatchSize"], out var batchSize) ? batchSize : 1000;
            _attachmentBatchSize = int.TryParse(configuration["AppSettings:AzureStorage:Attachments:BatchSize"], out var batchSizeAtt) ? batchSizeAtt : 0;
            _genericAttachmentBatchSize = int.TryParse(configuration["AppSettings:AzureStorage:GenericAttachments:BatchSize"], out var batchSizeGA) ? batchSizeGA : 0;
            _woAttachmentBatchSize = int.TryParse(configuration["AppSettings:AzureStorage:WorkOrderAttachments:BatchSize"], out var batchSizeWOA) ? batchSizeWOA : 0;
        }

        public async Task<ExternalBinaryDataConfig> GetExternalBinaryConfig(string connectionString, int externalBinaryProviderId)
        {
            _logger.LogInformation($"Tesitng........................");

            ExternalBinaryDataConfig config = null;
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                var query = Query.SELECT_EXTERNAL_BINARY_DATA_CONFIGS.Replace("@id", externalBinaryProviderId.ToString());
                var q1 = context.ExternalBinaryDataConfigs.FromSqlRaw(query);
                config = await q1.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method: GetSetExternalBinaryConfig | ConnectionString:{connectionString} | ExternalBinaryId: {externalBinaryProviderId} | Message: {ex.Message}| StackTrace:{ex.StackTrace}");
            }

            return config;
        }

        #region Update attachments
        public async Task<bool> UpdateAttachments(string connectionString, int externalBinaryProviderId, int attachmentId)
        {
            bool isSuccess = true;
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);
                var query = Query.UPDATE_ATTACHMENT
                        .Replace("@externalBinaryId", externalBinaryProviderId.ToString())
                        .Replace("@attachmentId", attachmentId.ToString());
                int rowsAffected = await context.Database.ExecuteSqlRawAsync(query);
                if (rowsAffected <= 0)
                {
                    isSuccess = false;
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
            }
            return isSuccess;
        }

        public async Task<bool> UpdateGenericAttachments(string connectionString, int externalBinaryProviderId, int attachmentId)
        {
            bool isSuccess = true;
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                var query = Query.UPDATE_GENERIC_ATTACHMENT
                        .Replace("@externalBinaryId", externalBinaryProviderId.ToString())
                        .Replace("@attachmentId", attachmentId.ToString());
                    int rowsAffected = await context.Database.ExecuteSqlRawAsync(query);
                    if (rowsAffected <= 0)
                    {
                        isSuccess = false;
                    }
                
            }
            catch (Exception ex)
            {
                isSuccess = false;
            }
            return isSuccess;
        }

        public async Task<bool> UpdateWorkOrderAttachments(string connectionString, int externalBinaryProviderId, int attachmentId)
        {
            bool isSuccess = true;
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                var query = Query.UPDATE_WORKORDER_ATTACHMENT
                        .Replace("@externalBinaryId", externalBinaryProviderId.ToString())
                        .Replace("@attachmentId", attachmentId.ToString());
                    int rowsAffected = await context.Database.ExecuteSqlRawAsync(query);
                    if (rowsAffected <= 0)
                    {
                        isSuccess = false;
                    }
                
            }
            catch (Exception ex)
            {
                isSuccess = false;
            }
            return isSuccess;
        }

        #endregion

        #region Get top k remaining entries id list
        public async Task<List<int>> GetTopKRemainingAttachmentsEntries(string connectionString)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.Attachments
                                      .Where(a => a.AttachmentData != null)
                                      .OrderBy(a => a.Id)
                                      .Take(_attachmentBatchSize)
                                      .Select(a => a.Id)
                                      .ToListAsync();
                
                return idList;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<int>> GetTopKRemainingGenericAttachmentsEntries(string connectionString)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.GenericAttachments
                                      .Where(a => a.AttachmentData != null)
                                      .OrderBy(a => a.Id)
                                      .Take(_genericAttachmentBatchSize)
                                      .Select(a => a.Id)
                                      .ToListAsync();
                
                return idList;
            }
            catch (Exception ex)
            {
                throw; 
            }
        }

        public async Task<List<int>> GetTopKRemainingWorkOrderAttachmentsEntries(string connectionString)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.WorkOrderAttachments
                                      .Where(a => a.AttachmentData != null)
                                      .OrderBy(a => a.Id) 
                                      .Take(_woAttachmentBatchSize)
                                      .Select(a => a.Id)
                                      .ToListAsync();
                
                return idList;
            }
            catch (Exception ex)
            {
                // Consider logging the exception
                throw; // Re-throw the original exception to preserve the stack trace
            }
        }

        #endregion

        #region Get remaining entries count
        public async Task<int> GetRemainingAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.Attachments
                                              .CountAsync(a => a.AttachmentData != null);
                
                return count;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<int> GetRemainingGenericAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.GenericAttachments
                                              .CountAsync(a => a.AttachmentData != null);
                
                return count;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<int> GetRemainingWorkOrderAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.WorkOrderAttachments
                                              .CountAsync(a => a.AttachmentData != null);
               
                return count;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        #endregion

        #region Get total entries count
        public async Task<int> GetTotalAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.Attachments.CountAsync();
                
                return count;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<int> GetTotalGenericAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.GenericAttachments.CountAsync();
                
                return count;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<int> GetTotalWorkOrderAttachmentsEntriesCount(string connectionString)
        {
            try
            {
                int count = 0;
                using var context = _contextFactory.CreateDbContext(connectionString);

                count = await context.WorkOrderAttachments.CountAsync();
                
                return count;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion

        #region Get attachments data by range
        public async Task<List<AttachmentModel>> GetAttachmentsDataRange(string connectionString, string databaseName, List<int> chunk)
        {
            List<AttachmentModel> attachmentModels = new List<AttachmentModel>();
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                string idsList = string.Join(",", chunk);
                    var query = Query.SELECT_ALL_ATTACHMENTS.Replace("@idList", idsList);
                    attachmentModels = context.AttachmentModels.FromSqlRaw(query).ToList();
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method: GetAttachmentsDataRange | Database:{databaseName} | Message: {ex.Message}| StackTrace:{ex.StackTrace}");
            }

            return attachmentModels;
        }

        public async Task<List<AttachmentModel>> GetGenericAttachmentsDataRange(string connectionString, string databaseName, List<int> chunk)
        {
            List<AttachmentModel> attachmentModels = new List<AttachmentModel>();
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                string idsList = string.Join(",", chunk);
                    var query = Query.SELECT_ALL_GENERIC_ATTACHMENTS.Replace("@idList", idsList);
                    attachmentModels = context.AttachmentModels.FromSqlRaw(query).ToList();
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method: GetGenericAttachmentsDataRange | Database:{databaseName} | Message: {ex.Message}| StackTrace:{ex.StackTrace}");
            }

            return attachmentModels;
        }

        public async Task<List<AttachmentModel>> GetWorkOrderAttachmentsDataRange(string connectionString, string databaseName, List<int> chunk)
        {
            List<AttachmentModel> attachmentModels = new List<AttachmentModel>();
            try
            {
                using var context = _contextFactory.CreateDbContext(connectionString);

                string idsList = string.Join(",", chunk);
                    var query = Query.SELECT_ALL_WORK_ORDER_ATTACHMENTS.Replace("@idList", idsList);
                    attachmentModels = context.AttachmentModels.FromSqlRaw(query).ToList();
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Method: GetWorkOrderAttachmentsDataRange | Database:{ databaseName} | Message: {ex.Message}| StackTrace:{ex.StackTrace}");
            }

            return attachmentModels;
        }
        #endregion

        #region Get all newly implemented external binary attachment id list
        public async Task<List<int>> GetAllExternalAttachmentsEntries(string connectionString, int externalBinaryProviderId)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.Attachments
                                      .Where(a => a.AttachmentData == null && a.ExternalBinaryId == externalBinaryProviderId)
                                      .OrderBy(a => a.Id)
                                      .Select(a => a.Id)
                                      .ToListAsync();

                return idList;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<int>> GetAllExternalGenericAttachmentsEntries(string connectionString, int externalBinaryProviderId)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.GenericAttachments
                                      .Where(a => a.AttachmentData == null && a.ExternalBinaryId == externalBinaryProviderId)
                                      .OrderBy(a => a.Id)
                                      .Select(a => a.Id)
                                      .ToListAsync();

                return idList;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<int>> GetAllExternalWorkOrderAttachmentsEntries(string connectionString, int externalBinaryProviderId)
        {
            try
            {
                List<int> idList = new List<int>();

                using var context = _contextFactory.CreateDbContext(connectionString);

                idList = await context.WorkOrderAttachments
                                      .Where(a => a.AttachmentData == null && a.ExternalBinaryId==externalBinaryProviderId)
                                      .OrderBy(a => a.Id)
                                      .Select(a => a.Id)
                                      .ToListAsync();

                return idList;
            }
            catch (Exception ex)
            {
                // Consider logging the exception
                throw; // Re-throw the original exception to preserve the stack trace
            }
        }

        #endregion
    }

    public static class Query
    {
        public static string SELECT_EXTERNAL_BINARY_DATA_CONFIGS = "SELECT * FROM [dbo].[tblExternalBinaries] WHERE Id=@id";

        public const string SELECT_ALL_ATTACHMENTS = "SELECT Id,AttachmentData FROM [dbo].[tblAttachments] WHERE Id IN (@idList) AND AttachmentData IS NOT NULL";
        public const string SELECT_ALL_GENERIC_ATTACHMENTS = "SELECT Id,AttachmentData FROM [dbo].[tblGenericAttachments] WHERE Id IN (@idList) AND AttachmentData IS NOT NULL";
        public const string SELECT_ALL_WORK_ORDER_ATTACHMENTS = "SELECT Id,AttachmentData FROM [dbo].[tblWorkOrderAttachments]  WHERE Id IN (@idList) AND AttachmentData IS NOT NULL";

        public static string UPDATE_ATTACHMENT = "UPDATE [dbo].[tblAttachments] SET AttachmentData=NULL, ExternalBinaryId=@externalBinaryId WHERE Id=@attachmentId";
        public static string UPDATE_GENERIC_ATTACHMENT = "UPDATE [dbo].[tblGenericAttachments] SET AttachmentData=NULL, ExternalBinaryId=@externalBinaryId, Modified=GETDATE() WHERE Id=@attachmentId";
        public static string UPDATE_WORKORDER_ATTACHMENT = "UPDATE [dbo].[tblWorkOrderAttachments] SET AttachmentData=NULL, ExternalBinaryId=@externalBinaryId, Modified=GETDATE() WHERE Id=@attachmentId";
    }
}
