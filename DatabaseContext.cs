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
    public DbSet<TransactionEntity> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the relationship between Laundromat and BankEntity
        modelBuilder.Entity<Laundromat>()
            .HasOne(l => l.Bank)
            .WithMany(b => b.Laundromats)
            .HasForeignKey(l => l.bId);

        // Configure the relationship between Laundromat and TransactionEntity
        modelBuilder.Entity<TransactionEntity>()
            .HasOne(t => t.Laundromat)
            .WithMany(l => l.Transactions)
            .HasForeignKey(t => t.LaundromatId);

        // Specify existing table names (match your database exactly)
        modelBuilder.Entity<BankEntity>().ToTable("bank");
        modelBuilder.Entity<Laundromat>().ToTable("laundromat");
        modelBuilder.Entity<TransactionEntity>().ToTable("transaction");
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