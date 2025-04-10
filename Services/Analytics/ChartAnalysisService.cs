using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class ChartAnalysisService : BaseAnalysisService
    {
        public ChartAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            StatsPeriodType? periodType = GetMatchingStatsPeriodType(startDate, endDate);

            string periodKey = null;

            if (periodType == StatsPeriodType.Quarter)
            {
                periodKey = GetQuarterPeriodKey(startDate);
            }
            else if (periodType == StatsPeriodType.CompletedQuarters)
            {
                periodKey = "past-4-completed-quarters";
            }

            if (periodType.HasValue)
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var stats = await dbContext
                    .LaundromatStats.AsNoTracking()
                    .Where(s =>
                        laundromatIds.Contains(s.LaundromatId)
                        && s.PeriodType == periodType.Value
                        && (periodType != StatsPeriodType.Quarter || s.PeriodKey == periodKey)
                    )
                    .ToListAsync();

                if (
                    stats.Count > 0
                    && stats.Select(s => s.LaundromatId).Distinct().Count() == laundromatIds.Count
                )
                {
                    // We have stats for all laundromats
                    return stats
                        .Select(s => new ChartDataPoint
                        {
                            Label = s.LaundromatName ?? $"ID {s.LaundromatId}",
                            Value = s.TotalRevenue,
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
            DateTime? endDate
        )
        {
            string cacheKey =
                $"revenue_direct_{string.Join("_", laundromatIds.OrderBy(id => id))}_"
                + $"{startDate?.ToString("yyyyMMdd") ?? "null"}_"
                + $"{endDate?.ToString("yyyyMMdd") ?? "null"}";

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
            var sql =
                @$"
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
                    result.Add(
                        new ChartDataPoint
                        {
                            Label = reader.GetString(0),
                            Value = reader.GetDecimal(1),
                        }
                    );
                }
            }

            // Cache the result
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

            return result;
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTime(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIdList = laundromats.Select(l => l.kId).ToList();

            // Fetch transactions
            var transactions = await dbContext
                .Transactions.Where(t =>
                    laundromatIdList.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.amount != 0
                )
                .ToListAsync();

            var totalDays = (endDate - startDate).Value.TotalDays;
            var interval =
                totalDays >= 60 ? "month"
                : totalDays <= 7 ? "day"
                : "week";

            List<ChartDataPoint> result = new List<ChartDataPoint>();

            if (interval == "month")
            {
                // Generate all months between startDate and endDate
                var allMonths = Enumerable
                    .Range(
                        0,
                        (int)(
                            (endDate.Value.Year - startDate.Value.Year) * 12
                            + endDate.Value.Month
                            - startDate.Value.Month
                            + 1
                        )
                    )
                    .Select(i => startDate.Value.AddMonths(i))
                    .Select(d => new { Year = d.Year, Month = d.Month })
                    .ToList();

                // Group transactions by month
                var grouped = transactions
                    .GroupBy(t => new { t.date.Year, t.date.Month })
                    .ToDictionary(
                        g => new { g.Key.Year, g.Key.Month },
                        g => g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    );

                // Merge with all months
                result = allMonths
                    .Select(m => new ChartDataPoint
                    {
                        Label = $"{m.Year}-{m.Month:D2}",
                        Value = grouped.ContainsKey(m) ? grouped[m] : 0,
                    })
                    .ToList();
            }
            else if (interval == "week")
            {
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;

                // Generate all weeks between startDate and endDate
                var allWeeks = Enumerable
                    .Range(0, (int)totalDays / 7 + 1)
                    .Select(i => startDate.Value.AddDays(i * 7))
                    .Select(d => new
                    {
                        Year = d.Year,
                        Week = calendar.GetWeekOfYear(
                            d,
                            System.Globalization.CalendarWeekRule.FirstDay,
                            DayOfWeek.Monday
                        ),
                    })
                    .ToList();

                // Group transactions by week
                var grouped = transactions
                    .GroupBy(t => new
                    {
                        t.date.Year,
                        Week = calendar.GetWeekOfYear(
                            t.date,
                            System.Globalization.CalendarWeekRule.FirstDay,
                            DayOfWeek.Monday
                        ),
                    })
                    .ToDictionary(
                        g => new { g.Key.Year, g.Key.Week },
                        g => g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    );

                // Merge with all weeks
                result = allWeeks
                    .Select(w => new ChartDataPoint
                    {
                        Label = $"{w.Year}-W{w.Week:D2}",
                        Value = grouped.ContainsKey(w) ? grouped[w] : 0,
                    })
                    .ToList();
            }
            else if (interval == "day")
            {
                // Generate all days between startDate and endDate
                var allDays = Enumerable
                    .Range(0, (int)totalDays + 1)
                    .Select(i => startDate.Value.AddDays(i))
                    .ToList();

                // Group transactions by day
                var grouped = transactions
                    .GroupBy(t => t.date.Date)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    );

                // Merge with all days
                result = allDays
                    .Select(d => new ChartDataPoint
                    {
                        Label = d.ToString("yyyy-MM-dd"),
                        Value = grouped.ContainsKey(d) ? grouped[d] : 0,
                    })
                    .ToList();
            }

            return result;
        }
    }
}