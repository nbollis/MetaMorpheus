using System.Data.Entity;
using YeastyBois.Models;

namespace YeastyBois.Database;

/// <summary>
/// Connects to the database and initializes values
/// </summary>
public class YeastyBoiDbContext : DbContext
{
    public YeastyBoiDbContext()
        : base(@"Source=C:\\Users\\nboll\\Source\\Repos\\MetaMorpheus\\MetaMorpheus\\YeastyBois\\Resources\\Yeast.db; Version=3")
    {
        // TODO: Insert connection string
    }

    public virtual DbSet<DataSet> DataSets { get; set; }
    public virtual DbSet<DataFile> DataFiles { get; set; }
    public virtual DbSet<Results> Results { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        // set keys
        modelBuilder.Entity<DataSet>()
            .HasKey(e => e.DataSetId);

        modelBuilder.Entity<DataFile>()
            .HasKey(e => e.DataFileId);

        modelBuilder.Entity<Results>()
            .HasKey(e => e.ResultId);

        // configure properties


        // set relationships
        // TODO: Configure properties
    }
}