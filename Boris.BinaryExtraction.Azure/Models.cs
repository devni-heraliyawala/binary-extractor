using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public class AzureBSConfig
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        public string ContainerName { get; set; }
        public string ExternalPath { get; set; }
    }

    public class DatabaseSize
    {
        public string Name { get; set; }
        public double SizeMB { get; set; }
    }

    public class ExternalBinaryDataConfig
    {
        public int Id { get; set; }
        public string RootDirectoryPath { get; set; }
        public string? XmlDocument { get; set; }
    }

    public class AttachmentModel
    {
        public int Id { get; set; }
        public byte[]? AttachmentData { get; set; }
    }

    [Table("tblAttachments")] // Map to the actual database table name
    public class Attachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public byte[]? AttachmentData { get; set; }

        public int? ExternalBinaryId { get; set; }
    }

    [Table("tblGenericAttachments")] // Map to the actual database table name
    public class GenericAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public byte[]? AttachmentData { get; set; }

        public int? ExternalBinaryId { get; set; }
    }

    [Table("tblWorkOrderAttachments")] // Map to the actual database table name
    public class WorkOrderAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public byte[]? AttachmentData { get; set; }

        public int? ExternalBinaryId { get; set; }
    }
}
