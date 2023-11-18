//using System.Data.Entity;

using YeastyBois.Models;
using Microsoft.EntityFrameworkCore;

namespace YeastyBois.Database;

/// <summary>
/// Connects to the database and initializes values
/// </summary>
public class YeastyBoiDbContext : DbContext
{
    public YeastyBoiDbContext()
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        DbPath = Path.Combine(directory, "Resources", "Yeast.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    public string DbPath { get; }

    public virtual DbSet<DataSet> DataSets { get; set; }
    public virtual DbSet<DataFile> DataFiles { get; set; }
    public virtual DbSet<Results> Results { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
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

    //protected override void OnModelCreating(DbModelBuilder modelBuilder)
    //{
    //    // set keys
    //    modelBuilder.Entity<DataSet>()
    //        .HasKey(e => e.DataSetId);

    //    modelBuilder.Entity<DataFile>()
    //        .HasKey(e => e.DataFileId);

    //    modelBuilder.Entity<Results>()
    //        .HasKey(e => e.ResultId);

    //    // configure properties


    //    // set relationships
    //    // TODO: Configure properties
    //}
}