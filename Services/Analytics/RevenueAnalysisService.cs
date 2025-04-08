using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class RevenueAnalysisService : BaseAnalysisService
    {
        public RevenueAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public decimal CalculateRevenueFromTransactions(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t => Math.Abs(t.amount)) / 100;
        }

        public async Task<decimal> CalculateLaundromatsRevenue(
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
                // Fall back to direct transaction calculation if any required parameters are missing
                using var localDbContext = _dbContextFactory.CreateDbContext();

                var transactions = await localDbContext
                    .Transactions.Where(t =>
                        laundromatIds.Contains(t.LaundromatId)
                        && t.date >= startDate
                        && t.date <= endDate
                    )
                    .ToListAsync();

                return transactions.Sum(t => Math.Abs(t.amount)) / 100m;
            }

            // Create cache key for the request
            string cacheKey =
                $"revenue_{string.Join("_", laundromatIds.OrderBy(id => id))}_"
                + $"{startDate?.ToString("yyyyMMdd")}_"
                + $"{endDate?.ToString("yyyyMMdd")}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out decimal cachedResult))
            {
                return cachedResult;
            }

            StatsPeriodType? periodType = GetMatchingStatsPeriodType(startDate, endDate);
            string periodKey = periodType == StatsPeriodType.Quarter ?
                GetQuarterPeriodKey(startDate) : null;

            // If we identified a matching period, get stats for all requested laundromats
            if (periodType.HasValue)
            {
                using var dbContext = _dbContextFactory.CreateDbContext();

                // Optimization: Get stats for all laundromats in a single query
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
                    // Fall back to direct transaction calculation
                    var transactions = await dbContext
                        .Transactions.Where(t =>
                            laundromatIds.Contains(t.LaundromatId)
                            && t.date >= startDate
                            && t.date <= endDate
                        )
                        .ToListAsync();

                    decimal revenue = transactions.Sum(t => Math.Abs(t.amount)) / 100m;

                    // Cache the result for shorter time since it's a fallback
                    _cache.Set(cacheKey, revenue, TimeSpan.FromMinutes(30));
                    return revenue;
                }

                // If we have stats for all laundromats, sum up the revenue
                if (stats.Any())
                {
                    decimal totalRevenue = stats.Sum(s => s.TotalRevenue);

                    // Cache the result for longer time since it's from precomputed stats
                    _cache.Set(cacheKey, totalRevenue, TimeSpan.FromHours(1));
                    return totalRevenue;
                }
            }

            // No matching precalculated stats found, fall back to direct calculation
            using var fallbackDbContext = _dbContextFactory.CreateDbContext();
            var fallbackTransactions = await fallbackDbContext
                .Transactions.Where(t =>
                    laundromatIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                )
                .ToListAsync();

            decimal fallbackRevenue = fallbackTransactions.Sum(t => Math.Abs(t.amount)) / 100m;

            // Cache the result for shorter time since it's a fallback
            _cache.Set(cacheKey, fallbackRevenue, TimeSpan.FromMinutes(30));
            return fallbackRevenue;
        }
    }
}