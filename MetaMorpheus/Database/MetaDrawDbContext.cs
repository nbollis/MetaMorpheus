using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;
using Microsoft.EntityFrameworkCore;

namespace Database
{
    /// <summary>
    /// Database context for MetaDraw
    /// </summary>
    public class MetaDrawDbContext : DbContext
    {
        private static string _dbPath = Path.Combine(GlobalVariables.DataDir, "MetaDraw.db");
        private static bool _created = false;

        public MetaDrawDbContext()
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

        public DbSet<PsmFromTsv> Psms { get; set; }
        public DbSet<MsDataFile> MsDataFiles { get; set; }
        public DbSet<MsDataScan> MsDataScans { get; set; }
        public DbSet<LibrarySpectrum> LibrarySpectra { get; set; }
    }
}
