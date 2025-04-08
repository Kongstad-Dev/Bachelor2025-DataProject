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
                    && !string.IsNullOrEmpty(t.unitName)
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
    }
}