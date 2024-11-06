using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public class DataContext : DbContext
    {
        public DbSet<AttachmentModel> AttachmentModels { get; set; }
        public DbSet<ExternalBinaryDataConfig> ExternalBinaryDataConfigs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<GenericAttachment> GenericAttachments { get; set; }
        public DbSet<WorkOrderAttachment> WorkOrderAttachments { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options) { }
    }
}
