using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics;

public class ProgramTypeAnalysisService : BaseAnalysisService
{
        public ProgramTypeAnalysisService(
        IDbContextFactory<YourDbContext> dbContextFactory,
        IMemoryCache cache
    ) : base(dbContextFactory, cache)
    {
    }

    public async Task<List<ChartDataPoint>> ProgramTypeProgramFromTransactions(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var transactions = await dbContext
            .Transactions.AsNoTracking()
            .Where(t =>
                laundromatIds.Contains(t.LaundromatId) &&
                t.date >= startDate &&
                t.date <= endDate &&
                t.amount != 0
            )
            .ToListAsync();
        // Group by temperature and count occurrences
        var groupedByProgramType = transactions
            .GroupBy(t => t.programType)
            .OrderBy(g => g.Key)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key}",
                Value = g.Count(),
            })
            .ToList();
        Console.WriteLine($"Grouped Data: {string.Join(", ", groupedByProgramType.Select(g => $"{g.Label}: {g.Value}"))}");
        return groupedByProgramType;
    }
    
    public async Task<List<ChartDataPoint>> ProgramTypeProcentageFromTransactions(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        Console.WriteLine("Calculating temperature percentages...");

        // Fetch transactions matching the criteria
        var transactions = await dbContext
            .Transactions.AsNoTracking()
            .Where(t =>
                laundromatIds.Contains(t.LaundromatId) &&
                t.date >= startDate &&
                t.date <= endDate &&
                t.amount != 0
            )
            .ToListAsync();

        // Calculate total transactions
        int totalTransactions = transactions.Count;
        if (totalTransactions == 0)
        {
            Console.WriteLine("No transactions found for the given criteria.");
            return new List<ChartDataPoint>();
        }

        // Group by temperature and calculate percentages
        var groupedByProgramType = transactions
            .GroupBy(t => t.programType)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key}",
                Value = Math.Round((decimal)g.Count() / totalTransactions * 100, 2) // Calculate percentage
            })
            .ToList();
        
        return groupedByProgramType;
    }
}