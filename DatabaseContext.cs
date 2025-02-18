using Bach2025_nortecnortec.Database;
using Bach2025nortec.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetEnv;
using DataEntity = Bach2025_nortecnortec.Database.DataEntity;

public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options)
        : base(options)
    {
    }

    // Define your DbSets here
    public DbSet<Laundromat> Laundromat { get; set; }
    public DbSet<DataEntity> DataEntities { get; set; }
}

public class YourDbContextFactory : IDesignTimeDbContextFactory<YourDbContext>
{
    public YourDbContext CreateDbContext(string[] args)
    {
        Env.Load();
        var optionsBuilder = new DbContextOptionsBuilder<YourDbContext>();
        var connectionString = $"Server={Env.GetString("DATABASE_HOST")};Database={Env.GetString("DATABASE_NAME")};User={Env.GetString("DATABASE_USERNAME")};Password={Env.GetString("DATABASE_PASSWORD")};";
        optionsBuilder.UseMySQL(connectionString);

        return new YourDbContext(optionsBuilder.Options);
    }
}