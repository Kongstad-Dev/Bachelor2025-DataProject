using Bach2025_nortec.Database;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DataEntity = Bach2025_nortec.Database.DataEntity;

public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options)
        : base(options) { }

    // Define your DbSets here
    public DbSet<Laundromat> Laundromat { get; set; }
    public DbSet<DataEntity> DataEntities { get; set; }
    public DbSet<BankEntity> Bank { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the relationship between Laundromat and BankEntity
        modelBuilder.Entity<Laundromat>().HasOne(l => l.Bank).WithMany().HasForeignKey(l => l.bId);

        // Specify existing table names (match your database exactly)
        modelBuilder.Entity<BankEntity>().ToTable("bank");
        modelBuilder.Entity<Laundromat>().ToTable("laundromat");
    }
}

public class YourDbContextFactory : IDesignTimeDbContextFactory<YourDbContext>
{
    public YourDbContext CreateDbContext(string[] args)
    {
        Env.Load();
        var optionsBuilder = new DbContextOptionsBuilder<YourDbContext>();
        var connectionString =
            $"Server={Env.GetString("DATABASE_HOST")};Database={Env.GetString("DATABASE_NAME")};User={Env.GetString("DATABASE_USERNAME")};Password={Env.GetString("DATABASE_PASSWORD")};";
        optionsBuilder.UseMySQL(connectionString);

        return new YourDbContext(optionsBuilder.Options);
    }
}
