using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class MachineAnalysisService : BaseAnalysisService
    {
        public MachineAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
        )> getStackedMachineStarts(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            // Get first 10 laundromatids
            var firstLaundromatsIds = laundromatIds.Take(10).ToList();

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => firstLaundromatsIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            // Fetch transactions within the date range
            var transactions = await dbContext
                .Transactions.AsNoTracking()
                .Where(t =>
                    firstLaundromatsIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.amount != 0
                )
                .ToListAsync();

// Get all unique unitNames
            var uniqueUnitNames = transactions
                .Select(t => t.unitName)
                .Distinct()
                .Select(name => {
                    // Extract prefix and number parts with regex
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"^([^\d]+)(\d+)?");
                    string prefix = match.Groups[1].Value.Trim();
                    int number = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                    return new { Name = name, Prefix = prefix, Number = number };
                })
                .OrderBy(item => item.Prefix)
                .ThenBy(item => item.Number)
                .Select(item => item.Name)
                .ToList();

            // Group transactions by laundromat and unitName
            var groupedData = transactions
                .GroupBy(t => new { t.LaundromatId, t.unitName })
                .Select(g => new
                {
                    LaundromatId = g.Key.LaundromatId,
                    UnitName = g.Key.unitName,
                    Count = g.Count(),
                })
                .ToList();

            // Map grouped data to laundromat names and create a decimal[][] for unit counts
            var labels = laundromats.Select(l => l.name ?? $"ID {l.kId}").ToArray();
            var values = laundromats
                .Select(l =>
                    uniqueUnitNames
                        .Select(unitName =>
                            groupedData
                                .Where(g => g.LaundromatId == l.kId && g.UnitName == unitName)
                                .Sum(g => (decimal)g.Count)
                        )
                        .ToArray()
                )
                .ToArray();
            var unitNames = uniqueUnitNames.ToArray();

            return (labels, values, unitNames);
        }

        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
            )> getStackedMachineRevenue(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
             // Get first 10 laundromatids
            var firstLaundromatsIds = laundromatIds.Take(10).ToList();

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => firstLaundromatsIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            // Fetch transactions within the date range
            var transactions = await dbContext
                .Transactions.AsNoTracking()
                .Where(t =>
                    firstLaundromatsIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.amount != 0
                )
                .ToListAsync();

            // Get all unique unitNames
            var uniqueUnitNames = transactions
                .Select(t => t.unitName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            // Group transactions by laundromat and unitName
            var groupedData = transactions
                .GroupBy(t => new { t.LaundromatId, t.unitName })
                .Select(g => new
                {
                    LaundromatId = g.Key.LaundromatId,
                    UnitName = g.Key.unitName,
                    amount = g.Sum(t => Math.Abs(t.amount)) / 100m,
                })
                .ToList();

            // Map grouped data to laundromat names and create a decimal[][] for unit counts
            var labels = laundromats.Select(l => l.name ?? $"ID {l.kId}").ToArray();
            var values = laundromats
                .Select(l =>
                    uniqueUnitNames
                        .Select(unitName =>
                            groupedData
                                .Where(g => g.LaundromatId == l.kId && g.UnitName == unitName)
                                .Sum(g => g.amount)
                        )
                        .ToArray()
                )
                .ToArray();
            var unitNames = uniqueUnitNames.ToArray();

            return (labels, values, unitNames);
        }
    }
}