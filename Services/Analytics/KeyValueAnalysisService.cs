using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace BlazorTest.Services.Analytics
{
    public class KeyValueAnalysisService : BaseAnalysisService
    {
        public KeyValueAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValuesFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            if (
                laundromatIds == null
                || !laundromatIds.Any()
                || startDate == null
                || endDate == null
            )
            {
                // Fall back to regular calculation if any required parameters are missing
                return await GetKeyValues(laundromatIds, startDate, endDate);
            }

            // Create cache key for the request
            string cacheKey =
                $"keystats_{string.Join("_", laundromatIds.OrderBy(id => id))}_"
                + $"{startDate?.ToString("yyyyMMdd")}_"
                + $"{endDate?.ToString("yyyyMMdd")}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out List<KeyValuePair<string, decimal>> cachedResult))
            {
                return cachedResult;
            }

            StatsPeriodType? periodType = GetMatchingStatsPeriodType(startDate, endDate);
            string periodKey = periodType == StatsPeriodType.Quarter ?
                GetQuarterPeriodKey(startDate) : null;
            string periodName = GetPeriodName(periodType.GetValueOrDefault(), periodKey);

            // If we identified a matching period, get stats for all requested laundromats
            if (periodType.HasValue)
            {
                using var dbContext = _dbContextFactory.CreateDbContext();

                // Optimization: Get stats for all laundromats in a single query using compiled query
                var statsQuery = dbContext
                    .LaundromatStats.AsNoTracking()
                    .Where(s =>
                        laundromatIds.Contains(s.LaundromatId) && s.PeriodType == periodType.Value
                    );

                // For quarters, we need to filter by the period key
                if (periodType == StatsPeriodType.Quarter && !string.IsNullOrEmpty(periodKey))
                {
                    statsQuery = statsQuery.Where(s => s.PeriodKey == periodKey);
                }

                var stats = await statsQuery.ToListAsync();

                // Check if we have stats for all requested laundromats
                var foundLaundromatIds = stats.Select(s => s.LaundromatId).Distinct().ToList();
                var missingLaundromatIds = laundromatIds.Except(foundLaundromatIds).ToList();

                // If any laundromats are missing stats, fall back to on-demand calculation for consistency
                if (missingLaundromatIds.Any())
                {
                    // Fall back to on-demand calculation for all laundromats to ensure consistent results
                    var calculatedStats = await GetKeyValues(laundromatIds, startDate, endDate);
                    _cache.Set(cacheKey, calculatedStats, TimeSpan.FromMinutes(30));
                    return calculatedStats;
                }

                // If we have stats for all laundromats, aggregate them
                if (stats.Any())
                {
                    // Aggregate the stats - optimized with LINQ
                    var aggregation =
                        stats
                            .GroupBy(s => 1) // Group all together
                            .Select(g => new
                            {
                                TotalTransactions = g.Sum(s => s.TotalTransactions),
                                TotalRevenue = g.Sum(s => s.TotalRevenue),
                                WashingMachineTransactions = g.Sum(s =>
                                    s.WashingMachineTransactions
                                ),
                                DryerTransactions = g.Sum(s => s.DryerTransactions),
                            })
                            .FirstOrDefault()
                        ?? new
                        {
                            TotalTransactions = 0,
                            TotalRevenue = 0m,
                            WashingMachineTransactions = 0,
                            DryerTransactions = 0,
                        };

                    // Calculate derived stats
                    var totalTransactions = aggregation.TotalTransactions;
                    var totalRevenue = aggregation.TotalRevenue;
                    var washingMachineTransactions = aggregation.WashingMachineTransactions;
                    var dryerTransactions = aggregation.DryerTransactions;

                    var avgRevenue =
                        laundromatIds.Count > 0 ? totalRevenue / laundromatIds.Count : 0;
                    var avgTransactions =
                        laundromatIds.Count > 0
                            ? totalTransactions / (decimal)laundromatIds.Count
                            : 0;

                    // Format results with proper rounding
                    var result = new List<KeyValuePair<string, decimal>>
                    {
                        new KeyValuePair<string, decimal>(
                            "Total Revenue",
                            Math.Round(totalRevenue, 2)
                        ),
                        new KeyValuePair<string, decimal>(
                            "Average Revenue",
                            Math.Round(avgRevenue, 2)
                        ),
                        new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
                        new KeyValuePair<string, decimal>(
                            "Average Transactions",
                            Math.Round(avgTransactions, 2)
                        ),
                        new KeyValuePair<string, decimal>(
                            "Washer Start",
                            washingMachineTransactions
                        ),
                        new KeyValuePair<string, decimal>("Dryer Start", dryerTransactions),
                    };

                    // Cache the result for 1 hour
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
                    return result;
                }
            }

            // No matching precalculated stats found, fall back to on-demand calculation
            var fallbackStats = await GetKeyValues(laundromatIds, startDate, endDate);
            _cache.Set(cacheKey, fallbackStats, TimeSpan.FromMinutes(30));
            return fallbackStats;
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValues(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            Console.WriteLine(
                $"GetKeyValues called with laundromatIds: {JsonConvert.SerializeObject(laundromatIds)}, startDate: {startDate}, endDate: {endDate}"
            );
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Get total transactions and revenue in a single query
            var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

            var transactionStats =
                await dbContext
                    .Transactions.Where(t =>
                        laundromatIds.Contains(t.LaundromatId)
                        && t.date >= startDate
                        && t.date <= endDate
                        && t.amount != 0
                    )
                    .GroupBy(t => 1) // Group all together
                    .Select(g => new
                    {
                        TotalTransactions = g.Count(),
                        TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        DryerCount = g.Count(t => dryerUnitTypes.Contains(t.unitType)),
                        DryerRevenue = g.Sum(t =>
                            dryerUnitTypes.Contains(t.unitType) ? Math.Abs(t.amount) / 100m : 0m
                        ),
                    })
                    .FirstOrDefaultAsync()
                ?? new
                {
                    TotalTransactions = 0,
                    TotalRevenue = 0m,
                    DryerCount = 0,
                    DryerRevenue = 0m,
                };

            if (transactionStats.TotalTransactions == 0)
            {
                return new List<KeyValuePair<string, decimal>>
                {
                    new KeyValuePair<string, decimal>("Total Revenue", 0),
                    new KeyValuePair<string, decimal>("Average Revenue", 0),
                    new KeyValuePair<string, decimal>("Total Transactions", 0),
                    new KeyValuePair<string, decimal>("Average Transactions", 0),
                    new KeyValuePair<string, decimal>("Washer Start", 0),
                    new KeyValuePair<string, decimal>("Washer Start Price", 0),
                    new KeyValuePair<string, decimal>("Dryer Start", 0),
                    new KeyValuePair<string, decimal>("Dryer Start Price", 0),
                };
            }

            var totalTransactions = transactionStats.TotalTransactions;
            var totalRevenue = transactionStats.TotalRevenue;

            // Calculate derived metrics
            var avgRevenue = totalTransactions > 0 ? totalRevenue / laundromatIds.Count : 0;
            var avgTransactions =
                totalTransactions > 0 ? totalTransactions / laundromatIds.Count : 0;
            var washingMachineTransactions = totalTransactions - transactionStats.DryerCount;
            var dryerStartPrice = transactionStats.DryerRevenue / transactionStats.DryerCount;
            var washerStartPrice =
                (transactionStats.TotalRevenue - transactionStats.DryerRevenue)
                / washingMachineTransactions;

            // Return results
            return new List<KeyValuePair<string, decimal>>
            {
                new KeyValuePair<string, decimal>("Total Revenue", totalRevenue),
                new KeyValuePair<string, decimal>("Average Revenue", avgRevenue),
                new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
                new KeyValuePair<string, decimal>("Average Transactions", avgTransactions),
                new KeyValuePair<string, decimal>("Washer Start", washingMachineTransactions),
                new KeyValuePair<string, decimal>("Washer Start Price", washerStartPrice),
                new KeyValuePair<string, decimal>("Dryer Start", transactionStats.DryerCount),
                new KeyValuePair<string, decimal>("Dryer Start Price", dryerStartPrice),
            };
        }
    }
}