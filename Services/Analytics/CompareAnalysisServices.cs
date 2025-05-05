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

    public class MultiLineChartResult
    {
        public string[] Labels { get; set; } = Array.Empty<string>();
        public decimal[][] Values { get; set; } = Array.Empty<decimal[]>();
        public string[] DatasetLabels { get; set; } = Array.Empty<string>();
    }

    public async Task<Dictionary<string, List<ChartDataPoint>>> GetTransactionOverTimePerLaundromat(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate
    )
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Get laundromat names for mapping
        var nameMap = await db.Laundromat
            .Where(l => laundromatIds.Contains(l.kId))
            .ToDictionaryAsync(l => l.kId, l => l.name);

        // Get all transactions for selected laundromats in range
        var transactions = await db.Transactions
            .Where(t =>
                laundromatIds.Contains(t.LaundromatId) &&
                t.date >= startDate &&
                t.date <= endDate &&
                t.amount != 0)
            .ToListAsync();

        // Create the complete list of months between start and end
        var intervals = new List<DateTime>();
        var current = new DateTime(startDate!.Value.Year, startDate.Value.Month, 1);
        var last = new DateTime(endDate!.Value.Year, endDate.Value.Month, 1);

        while (current <= last)
        {
            intervals.Add(current);
            current = current.AddMonths(1);
        }

        // Create dictionary: laundromatName => list of ChartDataPoint
        var result = new Dictionary<string, List<ChartDataPoint>>();

        foreach (var laundromatId in laundromatIds)
        {
            var laundromatName = nameMap.TryGetValue(laundromatId, out var name) ? name : laundromatId;
            var dataPoints = new List<ChartDataPoint>();

            foreach (var interval in intervals)
            {
                var total = transactions
                    .Where(t =>
                        t.LaundromatId == laundromatId &&
                        t.date.Year == interval.Year &&
                        t.date.Month == interval.Month)
                    .Sum(t => Math.Abs(t.amount)) / 100m;

                dataPoints.Add(new ChartDataPoint
                {
                    Label = interval.ToString("yyyy-MM"), // X-axis label
                    Value = total
                });
            }

            result[laundromatName] = dataPoints;
        }

        return result;
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
                    x => x.Sum(t => Math.Abs(t.amount)) / 100m
                )
            );

        return grouped;
    }
    
    
    

    public async Task<MultiLineChartResult> GetTransactionChartData(
        List<string> laundromatIds,
        DateTime? startDate,
        DateTime? endDate)
    {
        var raw = await GetTransactionOverTimePerLaundromat(laundromatIds, startDate, endDate);

        var allLabels = raw.Values
            .SelectMany(list => list.Select(p => p.Label))
            .Distinct()
            .OrderBy(label => label)
            .ToArray();

        var datasets = raw.Values.Select(data =>
        {
            var dict = data.ToDictionary(p => p.Label, p => p.Value);
            return allLabels.Select(label => dict.TryGetValue(label, out var v) ? v : 0m).ToArray();
        }).ToArray();

        return new MultiLineChartResult
        {
            Labels = allLabels,
            Values = datasets,
            DatasetLabels = raw.Keys.ToArray() // <- uses names now
        };
    }
}
