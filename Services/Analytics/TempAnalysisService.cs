using BlazorTest.Database.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics;
public class TempAnalysisService : BaseAnalysisService
{
    public TempAnalysisService(
        IDbContextFactory<YourDbContext> dbContextFactory,
        IMemoryCache cache
    ) : base(dbContextFactory, cache)
    {
    }

    public async Task<List<ChartDataPoint>> TempProgramFromTransactions(
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
                t.temperature != 0
            )
            .ToListAsync();
        // Group by temperature and count occurrences
        var groupedByTemp = transactions
            .GroupBy(t => t.temperature)
            .OrderBy(g => g.Key)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key}°C",
                Value = g.Count(),
            })
            .ToList();
        Console.WriteLine($"Grouped Data: {string.Join(", ", groupedByTemp.Select(g => $"{g.Label}: {g.Value}"))}");
        return groupedByTemp;
    }
    
    public async Task<List<ChartDataPoint>> TempProgramProcentageFromTransactions(
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
                t.temperature != 0
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
        var groupedByTemp = transactions
            .GroupBy(t => t.temperature)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key}°C",
                Value = Math.Round((decimal)g.Count() / totalTransactions * 100, 2) // Calculate percentage
            })
            .ToList();

        Console.WriteLine($"Grouped Data with Percentages: {string.Join(", ", groupedByTemp.Select(g => $"{g.Label}: {g.Value}%"))}");
        return groupedByTemp;
    }
}