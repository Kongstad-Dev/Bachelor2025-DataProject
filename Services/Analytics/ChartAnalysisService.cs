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
        )
            : base(dbContextFactory, cache) { }

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

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTimeFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            if (!startDate.HasValue || !endDate.HasValue || laundromatIds.Count == 0)
            {
                return new List<ChartDataPoint>();
            }

            // OPTIMIZATION 1: Use string interpolation with format specifiers for cache key generation
            var orderedIds = string.Join("_", laundromatIds.OrderBy(id => id));
            string cacheKey =
                $"revenue_timeseries_{orderedIds}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";

            // Try to use cached data first
            if (_cache.TryGetValue(cacheKey, out List<ChartDataPoint> cachedResult))
            {
                return cachedResult;
            }

            // Try to find matching precalculated time series data
            StatsPeriodType? periodType = GetMatchingStatsPeriodType(
                startDate.Value,
                endDate.Value
            );

            if (periodType.HasValue)
            {
                string periodKey = GetPeriodKeyForStats(periodType.Value, startDate.Value);

                if (!string.IsNullOrEmpty(periodKey))
                {
                    using var dbContext = _dbContextFactory.CreateDbContext();

                    // Create an optimized query using regular LINQ
                    var query = dbContext
                        .LaundromatStats.AsNoTracking()
                        .Where(s =>
                            laundromatIds.Contains(s.LaundromatId)
                            && s.PeriodType == periodType.Value
                            && s.PeriodKey == periodKey
                            && (s.AvailableTimeSeriesData & TimeSeriesDataTypes.Revenue)
                                == TimeSeriesDataTypes.Revenue
                            && !string.IsNullOrEmpty(s.RevenueTimeSeriesData)
                        )
                        .Select(s => new { s.LaundromatId, s.RevenueTimeSeriesData });

                    // Execute the query properly with async support
                    var statsData =
                        laundromatIds.Count == 1
                            ? await query.Take(1).ToListAsync()
                            : await query.ToListAsync();

                    if (statsData.Count > 0)
                    {
                        try
                        {
                            // OPTIMIZATION 3: Process data in parallel for large datasets
                            Dictionary<string, decimal> aggregatedDict = null;
                            List<ChartDataPoint> singleLaundromatPoints = null;

                            if (laundromatIds.Count == 1)
                            {
                                // Single laundromat case - direct deserialization
                                var timeSeriesInfo =
                                    System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                        statsData[0].RevenueTimeSeriesData,
                                        new System.Text.Json.JsonSerializerOptions
                                        {
                                            PropertyNameCaseInsensitive = true,
                                        }
                                    );

                                if (timeSeriesInfo?.DataPoints?.Count > 0)
                                {
                                    singleLaundromatPoints = timeSeriesInfo
                                        .DataPoints.OrderBy(p => p.Label)
                                        .ToList();

                                    // Cache and return the result
                                    _cache.Set(
                                        cacheKey,
                                        singleLaundromatPoints,
                                        TimeSpan.FromHours(1)
                                    );
                                    return singleLaundromatPoints;
                                }
                            }
                            else
                            {
                                // OPTIMIZATION 4: Deserialize in parallel for multiple laundromats
                                aggregatedDict = new Dictionary<string, decimal>();
                                var lockObj = new object();

                                // Use Parallel.ForEach for large datasets (>10 laundromats)
                                if (statsData.Count > 10)
                                {
                                    Parallel.ForEach(
                                        statsData,
                                        stat =>
                                        {
                                            try
                                            {
                                                var timeSeriesInfo =
                                                    System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                                        stat.RevenueTimeSeriesData,
                                                        new System.Text.Json.JsonSerializerOptions
                                                        {
                                                            PropertyNameCaseInsensitive = true,
                                                        }
                                                    );

                                                if (timeSeriesInfo?.DataPoints?.Count > 0)
                                                {
                                                    // Thread-safe aggregate into dictionary
                                                    lock (lockObj)
                                                    {
                                                        foreach (
                                                            var point in timeSeriesInfo.DataPoints
                                                        )
                                                        {
                                                            if (
                                                                aggregatedDict.ContainsKey(
                                                                    point.Label
                                                                )
                                                            )
                                                                aggregatedDict[point.Label] +=
                                                                    point.Value;
                                                            else
                                                                aggregatedDict[point.Label] =
                                                                    point.Value;
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine(
                                                    $"Error deserializing time series for laundromat {stat.LaundromatId}: {ex.Message}"
                                                );
                                            }
                                        }
                                    );
                                }
                                else
                                {
                                    // Use sequential processing for smaller datasets
                                    foreach (var stat in statsData)
                                    {
                                        try
                                        {
                                            var timeSeriesInfo =
                                                System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                                    stat.RevenueTimeSeriesData,
                                                    new System.Text.Json.JsonSerializerOptions
                                                    {
                                                        PropertyNameCaseInsensitive = true,
                                                    }
                                                );

                                            if (timeSeriesInfo?.DataPoints?.Count > 0)
                                            {
                                                foreach (var point in timeSeriesInfo.DataPoints)
                                                {
                                                    if (aggregatedDict.ContainsKey(point.Label))
                                                        aggregatedDict[point.Label] += point.Value;
                                                    else
                                                        aggregatedDict[point.Label] = point.Value;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(
                                                $"Error deserializing time series for laundromat {stat.LaundromatId}: {ex.Message}"
                                            );
                                        }
                                    }
                                }

                                if (aggregatedDict.Count > 0)
                                {
                                    // OPTIMIZATION 5: Use capacity constructor for result list
                                    var result = new List<ChartDataPoint>(aggregatedDict.Count);

                                    // OPTIMIZATION 6: Use ToArray for better performance when ordering
                                    var orderedKeys = aggregatedDict.Keys.ToArray();
                                    Array.Sort(orderedKeys);

                                    foreach (var key in orderedKeys)
                                    {
                                        result.Add(
                                            new ChartDataPoint
                                            {
                                                Label = key,
                                                Value = aggregatedDict[key],
                                            }
                                        );
                                    }

                                    // Cache and return the result
                                    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
                                    return result;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error and fall back to original implementation
                            Console.WriteLine($"Error processing time series data: {ex.Message}");
                        }
                    }
                }
            }

            // OPTIMIZATION 7: For large datasets in the fallback path, use SQL directly
            var totalDays = (endDate.Value - startDate.Value).TotalDays;

            if (totalDays > 60 && laundromatIds.Count > 3)
            {
                var optimizedResult = await GetRevenueForLaundromatsOverTimeOptimized(
                    laundromatIds,
                    startDate.Value,
                    endDate.Value
                );
                if (optimizedResult.Count > 0)
                {
                    _cache.Set(cacheKey, optimizedResult, TimeSpan.FromHours(1));
                    return optimizedResult;
                }
            }

            // Fall back to original implementation
            var fallbackResult = await GetRevenueForLaundromatsOverTime(
                laundromatIds,
                startDate,
                endDate
            );

            // Cache the result
            _cache.Set(cacheKey, fallbackResult, TimeSpan.FromHours(1));

            return fallbackResult;
        }

        // OPTIMIZATION: Add new method for direct SQL aggregation for large datasets
        private async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTimeOptimized(
            List<string> laundromatIds,
            DateTime startDate,
            DateTime endDate
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var totalDays = (endDate - startDate).TotalDays;
            var interval =
                totalDays >= 60 ? "month"
                : totalDays <= 7 ? "day"
                : "week";

            // Create a comma-separated list of IDs for SQL query
            string idList = string.Join("','", laundromatIds.Select(id => id.Replace("'", "''")));
            var result = new List<ChartDataPoint>();

            if (interval == "month")
            {
                // Use direct SQL for monthly aggregation
                string sql =
                    @$"
            SELECT 
                CONCAT(YEAR(t.date), '-', LPAD(MONTH(t.date), 2, '0')) AS Label,
                SUM(ABS(t.amount)) / 100 AS Value
            FROM 
                transaction t
            WHERE 
                t.LaundromatId IN ('{idList}')
                AND t.date >= '{startDate:yyyy-MM-dd}'
                AND t.date <= '{endDate:yyyy-MM-dd}'
                AND t.amount != 0
            GROUP BY 
                YEAR(t.date), MONTH(t.date)
            ORDER BY
                YEAR(t.date), MONTH(t.date)";

                result = await ExecuteSqlForChartData(dbContext, sql);

                // Generate all months to ensure we have points for every month
                var allMonths = Enumerable
                    .Range(
                        0,
                        (int)(
                            (endDate.Year - startDate.Year) * 12
                            + endDate.Month
                            - startDate.Month
                            + 1
                        )
                    )
                    .Select(i => startDate.AddMonths(i))
                    .Select(d => $"{d.Year}-{d.Month:D2}")
                    .ToList();

                // Create a lookup of existing data
                var dataDict = result.ToDictionary(p => p.Label, p => p.Value);

                // Ensure all months are represented
                return allMonths
                    .Select(month => new ChartDataPoint
                    {
                        Label = month,
                        Value = dataDict.ContainsKey(month) ? dataDict[month] : 0,
                    })
                    .ToList();
            }
            else if (interval == "day")
            {
                string sql =
                    @$"
        SELECT 
            DATE_FORMAT(t.date, '%Y-%m-%d') AS Label,
            SUM(ABS(t.amount)) / 100 AS Value
        FROM 
            transaction t
        WHERE 
            t.LaundromatId IN ('{idList}')
            AND t.date >= '{startDate:yyyy-MM-dd}'
            AND t.date <= '{endDate:yyyy-MM-dd}'
            AND t.amount != 0
        GROUP BY 
            DATE_FORMAT(t.date, '%Y-%m-%d')
        ORDER BY
            DATE_FORMAT(t.date, '%Y-%m-%d')";

                result = await ExecuteSqlForChartData(dbContext, sql);

                // Fill in missing days
                var allDays = Enumerable
                    .Range(0, (int)totalDays + 1)
                    .Select(i => startDate.AddDays(i).ToString("yyyy-MM-dd"))
                    .ToList();

                var dataDict = result.ToDictionary(p => p.Label, p => p.Value);

                return allDays
                    .Select(day => new ChartDataPoint
                    {
                        Label = day,
                        Value = dataDict.ContainsKey(day) ? dataDict[day] : 0,
                    })
                    .ToList();
            }

            // For week, use the original implementation as MySQL week calculations can be complex
            return new List<ChartDataPoint>();
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTime(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            // Validate required parameters
            if (laundromatIds == null || !laundromatIds.Any())
            {
            // Return empty result instead of throwing
            return new List<ChartDataPoint>();
            }

            if (startDate == null)
            {
                throw new ArgumentNullException(nameof(startDate), "Start date is required");
            }

            if (endDate == null)
            {
                throw new ArgumentNullException(nameof(endDate), "End date is required");
            }

            if (startDate > endDate)
            {
                throw new ArgumentException("Start date must be before or equal to end date");
            }

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            if (!laundromats.Any())
            {
                throw new ArgumentException(
                    "None of the provided laundromat IDs were found",
                    nameof(laundromatIds)
                );
            }

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

                var lastWeek = new {
                Year = endDate.Value.Year,
                Week = calendar.GetWeekOfYear(
                    endDate.Value,
                    System.Globalization.CalendarWeekRule.FirstDay,
                    DayOfWeek.Monday
                )
                };

                // Add the week containing the endDate if it's not already included
                if (!allWeeks.Any(w => w.Year == lastWeek.Year && w.Week == lastWeek.Week))
                {
                    allWeeks.Add(lastWeek);
                }

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

        private async Task<List<ChartDataPoint>> ExecuteSqlForChartData(
            YourDbContext dbContext,
            string sql
        )
        {
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

            return result;
        }
    }
}
