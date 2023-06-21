using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MetaDrawBackend.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace MetaDrawBackend.DependencyInjection
{
    /// <summary>
    /// Database context
    /// </summary>
    public class SampleDBContext : DbContext
    {
        private static string _dbPath = Path.Combine(GlobalVariables.DataDir, "Sample.db");
        private static bool _created = false;
        public SampleDBContext()
        {
            // Ensures a fresh database is created the first time the
            // database connection is constructed each time the program is ran
            if (!_created)
            {
                _created = true;
                Database.EnsureDeleted();
                Database.EnsureCreated();
            }
        }

        /// <summary>
        /// Used for configuring database context. In this case, we are telling it to use SqLite
        /// and providing a connection string to tell it which database to access
        /// </summary>
        /// <param name="optionbuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionbuilder)
        {
            optionbuilder.UseSqlite($"DataSource={_dbPath}");
        }

        public DbSet<Category> Categories { get; set; }
    }
}
