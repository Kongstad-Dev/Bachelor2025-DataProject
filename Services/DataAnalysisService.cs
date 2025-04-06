using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using BlazorTest.Database;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services
{
    public class DataAnalysisService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly IMemoryCache _cache;


        public DataAnalysisService(IDbContextFactory<YourDbContext> dbContextFactory, IMemoryCache cache)
        {
            _dbContextFactory = dbContextFactory;
            _cache = cache;

        }

        public class SoapResults
        {
            public decimal soap1 { get; set; }
            public decimal soap2 { get; set; }
            public decimal soap3 { get; set; }
        }
        

        private bool DateEquals(DateTime date1, DateTime date2)
        {
            return date1.Date == date2.Date;
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValuesFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            if (laundromatIds == null || !laundromatIds.Any() || startDate == null || endDate == null)
            {
                // Fall back to regular calculation if any required parameters are missing
                return await GetKeyValues(laundromatIds, startDate, endDate);
            }

            // Create cache key for the request
            string cacheKey = $"keystats_{string.Join("_", laundromatIds.OrderBy(id => id))}_" +
                             $"{startDate?.ToString("yyyyMMdd")}_" +
                             $"{endDate?.ToString("yyyyMMdd")}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out List<KeyValuePair<string, decimal>> cachedResult))
            {
                return cachedResult;
            }

            // Check if we have exact matches for standard periods
            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000
            var oneMonthAgo = startOfToday.AddMonths(-1);
            var sixMonthsAgo = startOfToday.AddMonths(-6);
            var yearAgo = startOfToday.AddYears(-1);

            using var dbContext = _dbContextFactory.CreateDbContext();
            StatsPeriodType? periodType = null;
            string periodKey = null;
            string periodName = "Custom";

            // CASE 1: Check for Month period match
            if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, oneMonthAgo))
            {
                periodType = StatsPeriodType.Month;
                periodName = "Last Month";
            }
            // CASE 2: Check for HalfYear period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, sixMonthsAgo))
            {
                periodType = StatsPeriodType.HalfYear;
                periodName = "Last 6 Months";
            }
            // CASE 3: Check for Year period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, yearAgo))
            {
                periodType = StatsPeriodType.Year;
                periodName = "Last Year";
            }
            // CASE 4: Check for Quarter matches
            else
            {
                // Calculate the current quarter details
                int currentQuarter = (now.Month + 2) / 3;
                int currentYear = now.Year;

                // Check current and previous quarters
                for (int i = 0; i < 4; i++)
                {
                    int offset = i;
                    int quarter = currentQuarter - (offset % 4);
                    int yearOffset = offset / 4;

                    if (quarter <= 0)
                    {
                        quarter += 4;
                        yearOffset++;
                    }

                    int year = currentYear - yearOffset;

                    // Calculate quarter start and end dates
                    int startMonth = (quarter - 1) * 3 + 1;
                    var quarterStartDate = new DateTime(year, startMonth, 1);
                    var quarterEndDate = quarterStartDate.AddMonths(3).AddDays(-1).Date.AddDays(1).AddMilliseconds(-1);

                    // Check if exact match (date only)
                    if (DateEquals(startDate.Value, quarterStartDate) && DateEquals(endDate.Value, quarterEndDate))
                    {
                        periodType = StatsPeriodType.Quarter;
                        periodKey = $"{year}-Q{quarter}";
                        periodName = $"Q{quarter} {year}";
                        break;
                    }
                }
            }

            // If we identified a matching period, get stats for all requested laundromats
            if (periodType.HasValue)
            {
                // Optimization: Get stats for all laundromats in a single query using compiled query
                var statsQuery = dbContext.LaundromatStats
                    .AsNoTracking()
                    .Where(s => laundromatIds.Contains(s.LaundromatId) && s.PeriodType == periodType.Value);

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
                    // Log detail about which laundromats are missing stats
                    //System.Console.WriteLine($"Missing stats for {missingLaundromatIds.Count} laundromats for period {periodType}: {string.Join(", ", missingLaundromatIds)}");

                    // Fall back to on-demand calculation for all laundromats to ensure consistent results
                    var calculatedStats = await GetKeyValues(laundromatIds, startDate, endDate);
                    _cache.Set(cacheKey, calculatedStats, TimeSpan.FromMinutes(30));
                    return calculatedStats;
                }

                // If we have stats for all laundromats, aggregate them
                if (stats.Any())
                {
                    // Aggregate the stats - optimized with LINQ
                    var aggregation = stats
                        .GroupBy(s => 1) // Group all together
                        .Select(g => new
                        {
                            TotalTransactions = g.Sum(s => s.TotalTransactions),
                            TotalRevenue = g.Sum(s => s.TotalRevenue),
                            WashingMachineTransactions = g.Sum(s => s.WashingMachineTransactions),
                            DryerTransactions = g.Sum(s => s.DryerTransactions)
                        })
                        .FirstOrDefault() ?? new
                        {
                            TotalTransactions = 0,
                            TotalRevenue = 0m,
                            WashingMachineTransactions = 0,
                            DryerTransactions = 0
                        };

                    // Calculate derived stats
                    var totalTransactions = aggregation.TotalTransactions;
                    var totalRevenue = aggregation.TotalRevenue;
                    var washingMachineTransactions = aggregation.WashingMachineTransactions;
                    var dryerTransactions = aggregation.DryerTransactions;

                    var avgRevenue = laundromatIds.Count > 0 ? totalRevenue / laundromatIds.Count : 0;
                    var avgTransactions = laundromatIds.Count > 0 ? totalTransactions / (decimal)laundromatIds.Count : 0;
                    var washingPercentage = totalTransactions > 0 ?
                        (decimal)washingMachineTransactions / totalTransactions * 100 : 0;
                    var dryerPercentage = totalTransactions > 0 ?
                        (decimal)dryerTransactions / totalTransactions * 100 : 0;

                    // Format results with proper rounding
                    var result = new List<KeyValuePair<string, decimal>>
            {
                new KeyValuePair<string, decimal>("Total Revenue", Math.Round(totalRevenue, 2)),
                new KeyValuePair<string, decimal>("Average Revenue", Math.Round(avgRevenue, 2)),
                new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
                new KeyValuePair<string, decimal>("Average Transactions", Math.Round(avgTransactions, 2)),
                new KeyValuePair<string, decimal>("Washer Start", washingMachineTransactions),
                new KeyValuePair<string, decimal>("Dryer Start", dryerTransactions)
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

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValues(List<string> laundromatIds, DateTime? startDate, DateTime? endDate)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Get total transactions and revenue in a single query
            var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

            var transactionStats = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                       t.date >= startDate &&
                       t.date <= endDate &&
                       t.amount != 0)
                .GroupBy(t => 1) // Group all together
                .Select(g => new
                {
                    TotalTransactions = g.Count(),
                    TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                    DryerCount = g.Count(t => dryerUnitTypes.Contains(t.unitType)),
                })
                .FirstOrDefaultAsync() ?? new { TotalTransactions = 0, TotalRevenue = 0m, DryerCount = 0 };

            var totalTransactions = transactionStats.TotalTransactions;
            var totalRevenue = transactionStats.TotalRevenue;

            // Calculate derived metrics
            var avgRevenue = totalTransactions > 0 ? totalRevenue / laundromatIds.Count : 0;
            var avgTransactions = totalTransactions > 0 ? totalTransactions / laundromatIds.Count : 0;
            var washingMachineTransactions = totalTransactions - transactionStats.DryerCount;

            // Return results
            return new List<KeyValuePair<string, decimal>>
    {
        new KeyValuePair<string, decimal>("Total Revenue", totalRevenue),
        new KeyValuePair<string, decimal>("Average Revenue", avgRevenue),
        new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
        new KeyValuePair<string, decimal>("Average Transactions", avgTransactions),
        new KeyValuePair<string, decimal>("Washer Start", washingMachineTransactions),
        new KeyValuePair<string, decimal>("Dryer Start", transactionStats.DryerCount)
    };
        }

        public decimal CalculateTotalSoapProgramFromTransactions(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t => (Convert.ToDecimal(t.soap)));
        }

        public decimal CalculateRevenueFromTransactions(List<TransactionEntity> transactions)
        {

            return transactions.Sum(t => Math.Abs(t.amount)) / 100;
        }


        public decimal CalculateAvgSecoundsFromTransactions(List<TransactionEntity> transactions)
        {
            var filtered = transactions.Where(t => t.seconds > 0).ToList();

            if (!filtered.Any())
                return 0;

            return filtered.Average(t => Convert.ToDecimal(t.seconds)) / 60; // return in minutes
        }

        public async Task<decimal> CalculateLaundromatsRevenue(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            if (laundromatIds == null || !laundromatIds.Any() || startDate == null || endDate == null)
            {
                // Fall back to direct transaction calculation if any required parameters are missing
                using var localDbContext = _dbContextFactory.CreateDbContext();

                var transactions = await localDbContext.Transactions
                    .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                           t.date >= startDate &&
                           t.date <= endDate)
                    .ToListAsync();

                return transactions.Sum(t => Math.Abs(t.amount)) / 100m;
            }

            // Create cache key for the request
            string cacheKey = $"revenue_{string.Join("_", laundromatIds.OrderBy(id => id))}_" +
                             $"{startDate?.ToString("yyyyMMdd")}_" +
                             $"{endDate?.ToString("yyyyMMdd")}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out decimal cachedResult))
            {
                return cachedResult;
            }

            // Check if we have exact matches for standard periods
            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000
            var oneMonthAgo = startOfToday.AddMonths(-1);
            var sixMonthsAgo = startOfToday.AddMonths(-6);
            var yearAgo = startOfToday.AddYears(-1);

            using var dbContext = _dbContextFactory.CreateDbContext();
            StatsPeriodType? periodType = null;
            string periodKey = null;

            // CASE 1: Check for Month period match
            if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, oneMonthAgo))
            {
                periodType = StatsPeriodType.Month;
            }
            // CASE 2: Check for HalfYear period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, sixMonthsAgo))
            {
                periodType = StatsPeriodType.HalfYear;
            }
            // CASE 3: Check for Year period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, yearAgo))
            {
                periodType = StatsPeriodType.Year;
            }
            // CASE 4: Check for Quarter matches
            else
            {
                // Calculate the current quarter details
                int currentQuarter = (now.Month + 2) / 3;
                int currentYear = now.Year;

                // Check current and previous quarters
                for (int i = 0; i < 4; i++)
                {
                    int offset = i;
                    int quarter = currentQuarter - (offset % 4);
                    int yearOffset = offset / 4;

                    if (quarter <= 0)
                    {
                        quarter += 4;
                        yearOffset++;
                    }

                    int year = currentYear - yearOffset;

                    // Calculate quarter start and end dates
                    int startMonth = (quarter - 1) * 3 + 1;
                    var quarterStartDate = new DateTime(year, startMonth, 1);
                    var quarterEndDate = quarterStartDate.AddMonths(3).AddDays(-1).Date.AddDays(1).AddMilliseconds(-1);

                    // Check if exact match (date only)
                    if (DateEquals(startDate.Value, quarterStartDate) && DateEquals(endDate.Value, quarterEndDate))
                    {
                        periodType = StatsPeriodType.Quarter;
                        periodKey = $"{year}-Q{quarter}";
                        break;
                    }
                }
            }

            // If we identified a matching period, get stats for all requested laundromats
            if (periodType.HasValue)
            {
                // Optimization: Get stats for all laundromats in a single query
                var statsQuery = dbContext.LaundromatStats
                    .AsNoTracking()
                    .Where(s => laundromatIds.Contains(s.LaundromatId) && s.PeriodType == periodType.Value);

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
                    // Log detail about which laundromats are missing stats
                    //System.Console.WriteLine($"Missing stats for {missingLaundromatIds.Count} laundromats for period {periodType}: {string.Join(", ", missingLaundromatIds)}");

                    // Fall back to direct transaction calculation
                    var transactions = await dbContext.Transactions
                        .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                               t.date >= startDate &&
                               t.date <= endDate)
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
            var fallbackTransactions = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                       t.date >= startDate &&
                       t.date <= endDate)
                .ToListAsync();

            decimal fallbackRevenue = fallbackTransactions.Sum(t => Math.Abs(t.amount)) / 100m;

            // Cache the result for shorter time since it's a fallback
            _cache.Set(cacheKey, fallbackRevenue, TimeSpan.FromMinutes(30));
            return fallbackRevenue;
        }

        public class ChartDataPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
        }

        // For GetRevenueForLaundromats, you can add a new method that uses stats:
        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000
            var oneMonthAgo = startOfToday.AddMonths(-1);
            var sixMonthsAgo = startOfToday.AddMonths(-6);
            var yearAgo = startOfToday.AddYears(-1);

            using var dbContext = _dbContextFactory.CreateDbContext();
            StatsPeriodType? periodType = null;
            string periodKey = null;

            // CASE 1: Check for Month period match
            if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, oneMonthAgo))
            {
                periodType = StatsPeriodType.Month;
            }
            // CASE 2: Check for HalfYear period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, sixMonthsAgo))
            {
                periodType = StatsPeriodType.HalfYear;
            }
            // CASE 3: Check for Year period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, yearAgo))
            {
                periodType = StatsPeriodType.Year;
            }
            // CASE 4: Check for Quarter matches
            else
            {
                // Calculate the current quarter details
                int currentQuarter = (now.Month + 2) / 3;
                int currentYear = now.Year;

                // Check current and previous quarters
                for (int i = 0; i < 4; i++)
                {
                    int offset = i;
                    int quarter = currentQuarter - (offset % 4);
                    int yearOffset = offset / 4;

                    if (quarter <= 0)
                    {
                        quarter += 4;
                        yearOffset++;
                    }

                    int year = currentYear - yearOffset;

                    // Calculate quarter start and end dates
                    int startMonth = (quarter - 1) * 3 + 1;
                    var quarterStartDate = new DateTime(year, startMonth, 1);
                    var quarterEndDate = quarterStartDate.AddMonths(3).AddDays(-1).Date.AddDays(1).AddMilliseconds(-1);

                    // Check if exact match (date only)
                    if (DateEquals(startDate.Value, quarterStartDate) && DateEquals(endDate.Value, quarterEndDate))
                    {
                        periodType = StatsPeriodType.Quarter;
                        periodKey = $"{year}-Q{quarter}";
                        break;
                    }
                }
            }

            if (periodType.HasValue)
            {
                var stats = await dbContext.LaundromatStats
                    .AsNoTracking()
                    .Where(s => laundromatIds.Contains(s.LaundromatId) &&
                           s.PeriodType == periodType.Value &&
                           (periodType != StatsPeriodType.Quarter || s.PeriodKey == periodKey))
                    .ToListAsync();

                if (stats.Count > 0 && stats.Select(s => s.LaundromatId).Distinct().Count() == laundromatIds.Count)
                {
                    // We have stats for all laundromats
                    return stats
                        .Select(s => new ChartDataPoint
                        {
                            Label = s.LaundromatName ?? $"ID {s.LaundromatId}",
                            Value = s.TotalRevenue
                        })
                        .ToList();
                }
            }

            // Fall back to original implementation
            return await GetRevenueForLaundromats(laundromatIds, startDate, endDate);
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            string cacheKey = $"revenue_direct_{string.Join("_", laundromatIds.OrderBy(id => id))}_" +
                             $"{startDate?.ToString("yyyyMMdd") ?? "null"}_" +
                             $"{endDate?.ToString("yyyyMMdd") ?? "null"}";

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out List<ChartDataPoint> cachedResult))
            {
                return cachedResult;
            }

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Create a comma-separated list of IDs for SQL query
            string idList = string.Join("','", laundromatIds.Select(id => id.Replace("'", "''")));
            string dateFilter = "";

            if (startDate.HasValue)
                dateFilter += $" AND t.date >= '{startDate.Value:yyyy-MM-dd}'";
            if (endDate.HasValue)
                dateFilter += $" AND t.date <= '{endDate.Value:yyyy-MM-dd}'";

            // Direct SQL query bypassing EF Core navigation issues
            var sql = @$"
        SELECT 
            COALESCE(l.name, CONCAT('ID ', l.kId)) AS Label,
            COALESCE(SUM(ABS(t.amount)), 0) / 100 AS Value
        FROM 
            laundromat l
        LEFT JOIN 
            transaction t ON l.kId = t.LaundromatId
            {(dateFilter.Length > 0 ? "AND " + dateFilter.Substring(4) : "")}
        WHERE 
            l.kId IN ('{idList}')
        GROUP BY 
            l.kId, l.name";

            var result = new List<ChartDataPoint>();

            using (var command = dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;

                if (dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                    await dbContext.Database.GetDbConnection().OpenAsync();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ChartDataPoint
                    {
                        Label = reader.GetString(0),
                        Value = reader.GetDecimal(1)
                    });
                }
            }

            // Cache the result
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

            return result;
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTime(List<string> laundromatIds, DateTime? startDate, DateTime? endDate)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIdList = laundromats.Select(l => l.kId).ToList();

            var transactions = await dbContext.Transactions
                .Where(t => laundromatIdList.Contains(t.LaundromatId) &&
                            t.date >= startDate &&
                            t.date <= endDate &&
                            t.amount != 0)
                .ToListAsync();
            var totalDays = (endDate - startDate).Value.TotalDays;
            var interval = (endDate - startDate).Value.TotalDays >= 60 ? "month" : totalDays <= 7 ? "day" : "week";

            List<ChartDataPoint> result = new List<ChartDataPoint>();

            if (interval == "month")
            {
                var grouped = transactions
                    .GroupBy(t => new { t.date.Year, t.date.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month)
                    .Select(g => new ChartDataPoint
                    {
                        Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Value = g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                    .ToList();

                result = grouped;
            }
            else if (interval == "week")    // interval == "week"
            {
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;

                var grouped = transactions
                    .GroupBy(t => new
                    {
                        t.date.Year,
                        Week = calendar.GetWeekOfYear(t.date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)
                    })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Week)
                    .Select(g => new ChartDataPoint
                    {
                        Label = $"{g.Key.Year}-W{g.Key.Week:D2}",
                        Value = g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                    .ToList();

                result = grouped;
            }
            else if (interval == "day")
            {
                var grouped = transactions
                    .GroupBy(t => new
                    {
                        t.date.Year,
                        t.date.Month,
                        t.date.Day

                    })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Day)
                    .Select(g => new ChartDataPoint
                    {
                        Label = $"{g.Key.Year}-D{g.Key.Day:D2}",
                        Value = g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100m

                    })
                    .ToList();
                result = grouped;
            }
            return result;
        }

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramFromTransactions(List<string> laundromatIds, DateTime? startDate, DateTime? endDate)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var transactions = await dbContext.Transactions
                .AsNoTracking()
                .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                            t.date >= startDate &&
                            t.date <= endDate)
                .ToListAsync();

            int soap1Count = transactions.Count(t => t.soap == 1);
            int soap2Count = transactions.Count(t => t.soap == 2);
            int soap3Count = transactions.Count(t => t.soap == 3);

            return new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Soap 1", Value = soap1Count },
                new ChartDataPoint { Label = "Soap 2", Value = soap2Count },
                new ChartDataPoint { Label = "Soap 3", Value = soap3Count }
            };
        }

public async Task<List<ChartDataPoint>> CalculateTransactionOverTime(List<string> laundromatIds,
    DateTime? startDate, DateTime? endDate)
{
    using var dbContext = _dbContextFactory.CreateDbContext();

    var laundromats = await dbContext.Laundromat
        .AsNoTracking()
        .Where(l => laundromatIds.Contains(l.kId))
        .Select(l => new { l.kId, l.name })
        .ToListAsync();

    var laundromatIdList = laundromats.Select(l => l.kId).ToList();

    var transactions = await dbContext.Transactions
        .Where(t => laundromatIdList.Contains(t.LaundromatId) &&
                    t.date >= startDate &&
                    t.date <= endDate &&
                    t.amount != 0)
        .ToListAsync();

    var totalDays = (endDate - startDate).Value.TotalDays;
    var interval = totalDays >= 60 ? "month" : totalDays <= 7 ? "day" : "week";

    List<ChartDataPoint> result = new List<ChartDataPoint>();

    if (interval == "month")
    {
        var grouped = transactions
            .GroupBy(t => new { t.date.Year, t.date.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Value = g.Count()
            })
            .ToList();

        result = grouped;
    }
    else if (interval == "week")
    {
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;

        var grouped = transactions
            .GroupBy(t => new
            {
                t.date.Year,
                Week = calendar.GetWeekOfYear(t.date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Week)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key.Year}-W{g.Key.Week:D2}",
                Value = g.Count()
            })
            .ToList();

        result = grouped;
    }
    else if (interval == "day")
    {
        var grouped = transactions
            .GroupBy(t => new
            {
                t.date.Year,
                t.date.Month,
                t.date.Day
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Day)
            .Select(g => new ChartDataPoint
            {
                Label = $"{g.Key.Year}-{g.Key.Day:D2}",
                Value = g.Count()
            })
            .ToList();

        result = grouped;
    }

    return result;
}

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var transactions = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                            t.date >= startDate &&
                            t.date <= endDate &&
                            t.soap > 0) // only valid soap usages
                .ToListAsync();

            int total = transactions.Count;

            int soap1Count = transactions.Count(t => t.soap == 1);
            int soap2Count = transactions.Count(t => t.soap == 2);
            int soap3Count = transactions.Count(t => t.soap == 3);

            decimal soap1Percent = total == 0 ? 0 : Math.Round((decimal)soap1Count / total * 100, 2);
            decimal soap2Percent = total == 0 ? 0 : Math.Round((decimal)soap2Count / total * 100, 2);
            decimal soap3Percent = total == 0 ? 0 : Math.Round(100 - soap1Percent - soap2Percent, 2); // adjust last one

            return new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Soap 1", Value = soap1Percent },
                new ChartDataPoint { Label = "Soap 2", Value = soap2Percent },
                new ChartDataPoint { Label = "Soap 3", Value = soap3Percent }
            };
        }



        public async Task<List<ChartDataPoint>> CalculateAvgSecoundsFromTransactions(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();


            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bankId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();


            var laundromatIds = laundromats.Select(l => l.kId).ToList();

            var transactions = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();
            // Group and compute revenue per laundromat
            var result = laundromats
                .GroupJoin(transactions,
                    l => l.kId,
                    t => t.LaundromatId,
                    (l, ts) => new ChartDataPoint
                    {
                        Label = l.name ?? $"ID {l.kId}",
                        Value = ts.Any()
                            ? ts.Average(t => Math.Abs(Convert.ToDecimal(t.seconds))) / 60
                            : 0

                    })
                .ToList();

            return result;
        }

    }
}