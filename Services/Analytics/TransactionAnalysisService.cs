using System.Collections.Concurrent;
using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class TransactionAnalysisService : BaseAnalysisService
    {
        public TransactionAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        )
            : base(dbContextFactory, cache) { }

        public decimal CalculateAvgSecoundsFromTransactions(List<TransactionEntity> transactions)
        {
            var filtered = transactions.Where(t => t.seconds > 0).ToList();

            if (!filtered.Any())
                return 0;

            return filtered.Average(t => Convert.ToDecimal(t.seconds)) / 60; // return in minutes
        }

        public async Task<List<ChartDataPoint>> CalculateTransactionOverTimeFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            if (!startDate.HasValue || !endDate.HasValue || laundromatIds.Count == 0)
            {
                return new List<ChartDataPoint>();
            }

            // OPTIMIZATION 1: More efficient cache key generation
            string cacheKey =
                $"transaction_timeseries_{string.Join("_", laundromatIds.OrderBy(id => id))}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";

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

                    // OPTIMIZATION 2: Use compiled query for better database performance
                    var statsQuery = EF.CompileQuery(
                        (YourDbContext ctx, List<string> ids, StatsPeriodType pt, string pk) =>
                            ctx
                                .LaundromatStats.AsNoTracking()
                                .Where(s =>
                                    ids.Contains(s.LaundromatId)
                                    && s.PeriodType == pt
                                    && s.PeriodKey == pk
                                    && (
                                        s.AvailableTimeSeriesData
                                        & TimeSeriesDataTypes.TransactionCount
                                    ) == TimeSeriesDataTypes.TransactionCount
                                    && !string.IsNullOrEmpty(s.TransactionCountTimeSeriesData)
                                )
                                .Select(s => new
                                {
                                    s.LaundromatId,
                                    s.TransactionCountTimeSeriesData,
                                })
                    );

                    // Execute the query with appropriate limiting
                    var statsData =
                        laundromatIds.Count == 1
                            ? statsQuery(dbContext, laundromatIds, periodType.Value, periodKey)
                                .Take(1)
                                .ToList()
                            : statsQuery(dbContext, laundromatIds, periodType.Value, periodKey)
                                .ToList();

                    if (statsData.Count > 0)
                    {
                        try
                        {
                            // Single workflow for both single and multiple laundromats
                            List<ChartDataPoint> result;

                            if (laundromatIds.Count == 1)
                            {
                                // Single laundromat case - direct deserialization
                                var timeSeriesInfo =
                                    System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                        statsData[0].TransactionCountTimeSeriesData,
                                        new System.Text.Json.JsonSerializerOptions
                                        {
                                            PropertyNameCaseInsensitive = true,
                                        }
                                    );

                                if (timeSeriesInfo?.DataPoints?.Count > 0)
                                {
                                    result = timeSeriesInfo
                                        .DataPoints.OrderBy(p => p.Label)
                                        .ToList();
                                    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
                                    return result;
                                }
                            }
                            else
                            {
                                // OPTIMIZATION 3: Process data in parallel for large datasets
                                Dictionary<string, decimal> aggregatedDict =
                                    new Dictionary<string, decimal>();

                                // Use parallel processing for large datasets (>10 laundromats)
                                if (statsData.Count > 10)
                                {
                                    ConcurrentDictionary<string, decimal> concurrentDict =
                                        new ConcurrentDictionary<string, decimal>();

                                    Parallel.ForEach(
                                        statsData,
                                        stat =>
                                        {
                                            try
                                            {
                                                var timeSeriesInfo =
                                                    System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                                        stat.TransactionCountTimeSeriesData,
                                                        new System.Text.Json.JsonSerializerOptions
                                                        {
                                                            PropertyNameCaseInsensitive = true,
                                                        }
                                                    );

                                                if (timeSeriesInfo?.DataPoints?.Count > 0)
                                                {
                                                    foreach (var point in timeSeriesInfo.DataPoints)
                                                    {
                                                        concurrentDict.AddOrUpdate(
                                                            point.Label,
                                                            point.Value,
                                                            (key, oldValue) =>
                                                                oldValue + point.Value
                                                        );
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine(
                                                    $"Error processing time series data for laundromat {stat.LaundromatId}: {ex.Message}"
                                                );
                                            }
                                        }
                                    );

                                    // Transfer to regular dictionary for final processing
                                    aggregatedDict = new Dictionary<string, decimal>(
                                        concurrentDict
                                    );
                                }
                                else
                                {
                                    // Sequential processing for smaller datasets
                                    foreach (var stat in statsData)
                                    {
                                        try
                                        {
                                            var timeSeriesInfo =
                                                System.Text.Json.JsonSerializer.Deserialize<TimeSeriesInfo>(
                                                    stat.TransactionCountTimeSeriesData,
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
                                                $"Error processing time series data for laundromat {stat.LaundromatId}: {ex.Message}"
                                            );
                                        }
                                    }
                                }

                                if (aggregatedDict.Count > 0)
                                {
                                    // OPTIMIZATION 4: Faster sorting with ToArray
                                    var orderedKeys = aggregatedDict.Keys.ToArray();
                                    Array.Sort(orderedKeys);

                                    result = new List<ChartDataPoint>(aggregatedDict.Count);
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

                                    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
                                    return result;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing time series data: {ex.Message}");
                        }
                    }
                }
            }

            // OPTIMIZATION 5: For large datasets, use direct SQL aggregation
            var totalDays = (endDate.Value - startDate.Value).TotalDays;
            if (totalDays > 60 && laundromatIds.Count > 3)
            {
                var optimizedResult = await CalculateTransactionOverTimeOptimized(
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
            var fallbackResult = await CalculateTransactionOverTime(
                laundromatIds,
                startDate,
                endDate
            );

            // Cache the result
            _cache.Set(cacheKey, fallbackResult, TimeSpan.FromHours(1));

            return fallbackResult;
        }

        private async Task<List<ChartDataPoint>> CalculateTransactionOverTimeOptimized(
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
                string sql = @$"
                SELECT 
                    formatted_date AS Label,
                    COUNT(*) AS Value
                FROM (
                    SELECT 
                        CONCAT(YEAR(date), '-', LPAD(MONTH(date), 2, '0')) AS formatted_date
                    FROM 
                        transaction
                    WHERE 
                        LaundromatId IN ('{idList}')
                        AND date >= '{startDate:yyyy-MM-dd}'
                        AND date <= '{endDate:yyyy-MM-dd}'
                        AND amount != 0
                ) AS subquery
                GROUP BY 
                    formatted_date
                ORDER BY
                    formatted_date";

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
                string sql = @$"
                SELECT 
                    formatted_date AS Label,
                    COUNT(*) AS Value
                FROM (
                    SELECT 
                        DATE_FORMAT(date, '%Y-%m-%d') AS formatted_date
                    FROM 
                        transaction
                    WHERE 
                        LaundromatId IN ('{idList}')
                        AND date >= '{startDate:yyyy-MM-dd}'
                        AND date <= '{endDate:yyyy-MM-dd}'
                        AND amount != 0
                ) AS subquery
                GROUP BY 
                    formatted_date
                ORDER BY
                    formatted_date";

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
            else if (interval == "week")
            {
                string sql = @$"
                SELECT 
                    week_key AS Label,
                    COUNT(*) AS Value
                FROM (
                    SELECT 
                        CONCAT(YEARWEEK(date, 1), '') AS week_key
                    FROM 
                        transaction
                    WHERE 
                        LaundromatId IN ('{idList}')
                        AND date >= '{startDate:yyyy-MM-dd}'
                        AND date <= '{endDate:yyyy-MM-dd}'
                        AND amount != 0
                ) AS subquery
                GROUP BY 
                    week_key
                ORDER BY
                    week_key";

                result = await ExecuteSqlForChartData(dbContext, sql);
            }

            // For weeks or empty results, return whatever we got
            return result;
        }

        // Add helper method for SQL execution
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

        // Optimize the fallback method for large datasets
        public async Task<List<ChartDataPoint>> CalculateTransactionOverTime(
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

            // OPTIMIZATION: Only fetch what we need
            var existingLaundromatIds = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => l.kId)
                .ToListAsync();

            if (existingLaundromatIds.Count == 0)
            {
                return new List<ChartDataPoint>();
            }

            var totalDays = (endDate.Value - startDate.Value).TotalDays;
            var interval =
                totalDays >= 60 ? "month"
                : totalDays <= 7 ? "day"
                : "week";

            // OPTIMIZATION: For large date ranges, use batched processing
            if (totalDays > 30 && existingLaundromatIds.Count > 3)
            {
                // Use direct SQL approach for large datasets
                return await CalculateTransactionOverTimeOptimized(
                    existingLaundromatIds,
                    startDate.Value,
                    endDate.Value
                );
            }

            // OPTIMIZATION: Directly group in database when possible
            if (interval == "day" || interval == "month")
            {
                // For day and month, we can use database grouping
                // Change this line to use a different variable name:
                var groupedResult = new List<ChartDataPoint>(); // Changed from 'result' to 'groupedResult'

                if (interval == "month")
                {
                    var monthlyData = await dbContext
                        .Transactions.Where(t =>
                            existingLaundromatIds.Contains(t.LaundromatId)
                            && t.date >= startDate
                            && t.date <= endDate
                            && t.amount != 0
                        )
                        .GroupBy(t => new { Year = t.date.Year, Month = t.date.Month })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Month,
                            Count = g.Count(),
                        })
                        .OrderBy(r => r.Year)
                        .ThenBy(r => r.Month)
                        .ToListAsync();

                    // Generate all months
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

                    var monthDict = monthlyData.ToDictionary(
                        r => new { r.Year, r.Month },
                        r => r.Count
                    );

                    return allMonths
                        .Select(m => new ChartDataPoint
                        {
                            Label = $"{m.Year}-{m.Month:D2}",
                            Value = monthDict.TryGetValue(new { m.Year, m.Month }, out var count)
                                ? count
                                : 0,
                        })
                        .ToList();
                }
                else if (interval == "day")
                {
                    var dailyData = await dbContext
                        .Transactions.Where(t =>
                            existingLaundromatIds.Contains(t.LaundromatId)
                            && t.date >= startDate
                            && t.date <= endDate
                            && t.amount != 0
                        )
                        .GroupBy(t => t.date.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .ToListAsync();

                    // Generate all days
                    var allDays = Enumerable
                        .Range(0, (int)totalDays + 1)
                        .Select(i => startDate.Value.AddDays(i))
                        .ToList();

                    var dayDict = dailyData.ToDictionary(d => d.Date, d => d.Count);

                    return allDays
                        .Select(d => new ChartDataPoint
                        {
                            Label = d.ToString("yyyy-MM-dd"),
                            Value = dayDict.TryGetValue(d.Date, out var count) ? count : 0,
                        })
                        .ToList();
                }
            }

            // For weeks or small datasets, use existing implementation
            // but with improved efficiency
            var transactions = await dbContext
                .Transactions.Where(t =>
                    existingLaundromatIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                )
                .Select(t => new { t.date }) // OPTIMIZATION: Only select what we need
                .AsNoTracking()
                .ToListAsync();

            List<ChartDataPoint> result = new List<ChartDataPoint>();

            if (interval == "week")
            {
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;

                // Generate all weeks between startDate and endDate
            var allWeeks = Enumerable
                .Range(0, (int)Math.Ceiling(totalDays / 7.0))
                .Select(i => startDate.Value.AddDays(i * 7))
                .Where(d => d <= endDate.Value)  // Ensure we don't go past endDate
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
                    .ToDictionary(g => new { g.Key.Year, g.Key.Week }, g => g.Count());

                // Merge with all weeks
                result = allWeeks
                    .Select(w => new ChartDataPoint
                    {
                        Label = $"{w.Year}-W{w.Week:D2}",
                        Value = grouped.ContainsKey(w) ? grouped[w] : 0,
                    })
                    .ToList();
            }

            return result;
        }

        public async Task<List<ChartDataPoint>> CalculateAvgSecoundsFromTransactions(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => l.bankId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIds = laundromats.Select(l => l.kId).ToList();

            var transactions = await dbContext
                .Transactions.Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();
            // Group and compute revenue per laundromat
            var result = laundromats
                .GroupJoin(
                    transactions,
                    l => l.kId,
                    t => t.LaundromatId,
                    (l, ts) =>
                        new ChartDataPoint
                        {
                            Label = l.name ?? $"ID {l.kId}",
                            Value = ts.Any()
                                ? ts.Average(t => Math.Abs(Convert.ToDecimal(t.seconds))) / 60
                                : 0,
                        }
                )
                .ToList();

            return result;
        }
    }
}
