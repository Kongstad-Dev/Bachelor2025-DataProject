using BlazorTest.Database;
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
        Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
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
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key}°C",
                Value = g.Count(),
            })
            .ToList();
        Console.WriteLine($"Grouped Data: {string.Join(", ", groupedByTemp.Select(g => $"{g.Label}: {g.Value}"))}");
        return groupedByTemp;
    }
}