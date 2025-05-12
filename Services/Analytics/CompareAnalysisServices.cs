using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics;

public class CompareAnalysisServices : BaseAnalysisService
{
    public CompareAnalysisServices(
        IDbContextFactory<YourDbContext> dbContextFactory,
        IMemoryCache cache
    ) : base(dbContextFactory, cache)
    {
    }

    public async Task<Dictionary<string, Dictionary<string, decimal>>> CalcTransactionOverTimeCompare(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        using var db = _dbContextFactory.CreateDbContext();

        var nameMap = await db.Laundromat
            .Where(l => laundromatIds.Contains(l.kId))
            .ToDictionaryAsync(l => l.kId, l => l.name);

        var transactions = await db.Transactions
            .Where(t =>
                laundromatIds.Contains(t.LaundromatId) &&
                t.date >= startDate &&
                t.date <= endDate)
            .ToListAsync();

        var grouped = transactions
            .GroupBy(t => new
            {
                t.LaundromatId,
                Month = new DateTime(t.date.Year, t.date.Month, 1)
            })
            .GroupBy(g => g.Key.LaundromatId)
            .ToDictionary(
                g => nameMap.TryGetValue(g.Key, out var name) ? name : g.Key,
                g => g.ToDictionary(
                    x => x.Key.Month.ToString("yyyy-MM"),
                    x => (decimal)x.Count()
                )
            );

        return grouped;
    }
    
    public async Task<Dictionary<string, Dictionary<string, decimal>>> CalcRevenueOverTimeCompare(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Map laundromat IDs to names
        var nameMap = await db.Laundromat
            .Where(l => laundromatIds.Contains(l.kId))
            .ToDictionaryAsync(l => l.kId, l => l.name);

        // Filter relevant transactions (non-zero and in range)
        var transactions = await db.Transactions
            .Where(t =>
                laundromatIds.Contains(t.LaundromatId) &&
                t.date >= startDate &&
                t.date <= endDate &&
                t.amount != 0)
            .ToListAsync();

        // Group and aggregate revenue per laundromat per month
        var grouped = transactions
            .GroupBy(t => new
            {
                t.LaundromatId,
                Month = new DateTime(t.date.Year, t.date.Month, 1)
            })
            .GroupBy(g => g.Key.LaundromatId)
            .ToDictionary(
                g => nameMap.TryGetValue(g.Key, out var name) ? name : g.Key,
                g => g.ToDictionary(
                    x => x.Key.Month.ToString("yyyy-MM"),
                    x => x.Sum(t => Math.Abs(t.amount)) / 100m // Convert øre to DKK
                )
            );

        return grouped;
    }
}
