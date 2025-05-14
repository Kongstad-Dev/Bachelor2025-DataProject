using BlazorTest.Database.entities;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options)
        : base(options) { }

    // Define your DbSets here
    public DbSet<Laundromat> Laundromat { get; set; }
    public DbSet<BankEntity> Bank { get; set; }
    public DbSet<TransactionEntity> Transactions { get; set; }
    public DbSet<LaundromatStats> LaundromatStats { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the relationship between Laundromat and BankEntity
        modelBuilder.Entity<Laundromat>()
            .HasOne(l => l.Bank)
            .WithMany(b => b.Laundromats)
            .HasForeignKey(l => l.bankId);

        // Configure the relationship between Laundromat and TransactionEntity
        modelBuilder.Entity<TransactionEntity>()
            .HasOne(t => t.Laundromat)
            .WithMany(l => l.Transactions)
            .HasForeignKey(t => t.LaundromatId);

        // Configure relationship between Laundromat and LaundromatStats
        modelBuilder.Entity<LaundromatStats>()
            .HasOne(s => s.Laundromat)
            .WithMany()
            .HasForeignKey(s => s.LaundromatId);

        // Configure the table names
        modelBuilder.Entity<BankEntity>().ToTable("bank");
        modelBuilder.Entity<Laundromat>().ToTable("laundromat");
        modelBuilder.Entity<TransactionEntity>().ToTable("transaction");
        modelBuilder.Entity<LaundromatStats>().ToTable("laundromat_stats");

        // ---- INDEXES FOR LAUNDROMAT TABLE ----

        // Index for bank ID lookups (finding all laundromats for a bank)
        modelBuilder.Entity<Laundromat>()
            .HasIndex(l => l.bankId)
            .HasDatabaseName("IX_Laundromat_BankId");

        // Index for lastFetchDate to quickly find laundromats needing updates
        modelBuilder.Entity<Laundromat>()
            .HasIndex(l => l.lastFetchDate)
            .HasDatabaseName("IX_Laundromat_LastFetchDate");

        // Index for geospatial queries (if you have any)
        modelBuilder.Entity<Laundromat>()
            .HasIndex(l => new { l.latitude, l.longitude })
            .HasDatabaseName("IX_Laundromat_Coordinates");

        // ---- INDEXES FOR TRANSACTION TABLE ----

        // Main index for filtering by laundromat
        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.LaundromatId)
            .HasDatabaseName("IX_Transaction_LaundromatId");

        // Combined index for date-based filtering by laundromat
        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => new { t.LaundromatId, t.date })
            .HasDatabaseName("IX_Transaction_LaundromatId_Date");

        // Index for filtering by machine type
        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.unitType)
            .HasDatabaseName("IX_Transaction_UnitType");

        // Index for revenue calculations
        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.amount)
            .HasDatabaseName("IX_Transaction_Amount");

        // Indexes for program analysis
        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.soap)
            .HasDatabaseName("IX_Transaction_Soap");

        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.temperature)
            .HasDatabaseName("IX_Transaction_Temperature");

        modelBuilder.Entity<TransactionEntity>()
            .HasIndex(t => t.programType)
            .HasDatabaseName("IX_Transaction_ProgramType");

        // Indexes for LaundromatStats
        modelBuilder.Entity<LaundromatStats>()
            .HasIndex(s => s.LaundromatId)
            .HasDatabaseName("IX_LaundromatStats_LaundromatId");

        modelBuilder.Entity<LaundromatStats>()
            .HasIndex(s => s.PeriodType)
            .HasDatabaseName("IX_LaundromatStats_PeriodType");

        modelBuilder.Entity<LaundromatStats>()
            .HasIndex(s => s.PeriodKey)
            .HasDatabaseName("IX_LaundromatStats_PeriodKey");

        // Combined index for efficient stats lookup
        modelBuilder.Entity<LaundromatStats>()
            .HasIndex(s => new { s.LaundromatId, s.PeriodType, s.PeriodKey })
            .HasDatabaseName("IX_LaundromatStats_Composite");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Load environment variables if needed
            DotNetEnv.Env.Load();

            // Get connection string
            var connectionString =
                $"Server={Env.GetString("DATABASE_HOST")};Database={Env.GetString("DATABASE_NAME")};User={Env.GetString("DATABASE_USERNAME")};Password={Env.GetString("DATABASE_PASSWORD")};";

            // Configure MySQL
            optionsBuilder.UseMySQL(connectionString);
        }

        // Performance optimizations

        // Don't track entities by default unless explicitly requested
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        // Enable batching of commands
        // (MySQL provider doesn't support this directly, but keeping for reference)
        // optionsBuilder.UseBatchSize(100);
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

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        optionsBuilder.UseMySQL(connectionString, mysqlOptions =>
        {
            mysqlOptions.CommandTimeout(60);
        });

        return new YourDbContext(optionsBuilder.Options);
    }
}