using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Boris.BinaryExtraction.Azure
{
    public class DataContextFactory
    {
        private readonly IConfiguration _configuration;

        public DataContextFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DataContext CreateDbContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(180); // Set timeout
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });

            return new DataContext(optionsBuilder.Options);
        }
    }
}
