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

        public SoapResults SoapCount(List<TransactionEntity> transactions)
        {
            var result = new SoapResults();

            foreach (var t in transactions)
            {
                var soap = Convert.ToInt32(t.soap); // safer cast

                switch (soap)
                {
                    case 1: result.soap1++; break;
                    case 2: result.soap2++; break;
                    case 3: result.soap3++; break;
                    default: break; // optionally throw/log unknown values
                }
            }

            return result;
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValues(List<string> laundromatIds, DateTime? startDate, DateTime? endDate)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Get total transactions and revenue in a single query
            var transactionStats = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId) &&
                       t.date >= startDate &&
                       t.date <= endDate &&
                       t.seconds != 0)
                .GroupBy(t => 1) // Group all together
                .Select(g => new
                {
                    TotalTransactions = g.Count(),
                    TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                    DryerCount = g.Count(t => new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 }.Contains(t.unitType))
                })
                .FirstOrDefaultAsync() ?? new { TotalTransactions = 0, TotalRevenue = 0m, DryerCount = 0 };

            var totalTransactions = transactionStats.TotalTransactions;
            var totalRevenue = transactionStats.TotalRevenue;

            // Calculate derived metrics
            var avgRevenue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;
            var avgTransactions = totalTransactions > 0 ? totalTransactions / laundromatIds.Count : 0;
            var dryerPercentage = totalTransactions > 0 ? (decimal)transactionStats.DryerCount / totalTransactions * 100 : 0;
            var washingPercentage = 100 - dryerPercentage;

            // Return results
            return new List<KeyValuePair<string, decimal>>
    {
        new KeyValuePair<string, decimal>("Total Revenue", totalRevenue),
        new KeyValuePair<string, decimal>("Average Revenue", avgRevenue),
        new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
        new KeyValuePair<string, decimal>("Average Transactions", avgTransactions),
        new KeyValuePair<string, decimal>("Washing Machine %", washingPercentage),
        new KeyValuePair<string, decimal>("Dryer %", dryerPercentage)
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

        public decimal CalculateLaundromatsRevenue(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t => Math.Abs(t.amount)) / 100;
        }

        public class ChartDataPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
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
                            t.amount >= 1)
                .ToListAsync();

            var interval = (endDate - startDate).Value.TotalDays > 30 ? "month" : "week";

            List<ChartDataPoint> result;

            if (interval == "month")
            {
                var grouped = transactions
                    .GroupBy(t => new { t.date.Year, t.date.Month })
                    .Select(g => new ChartDataPoint
                    {
                        Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Value = g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                    .ToList();

                result = grouped;
            }
            else // interval == "week"
            {
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;

                var grouped = transactions
                    .GroupBy(t => new
                    {
                        t.date.Year,
                        Week = calendar.GetWeekOfYear(t.date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)
                    })
                    .Select(g => new ChartDataPoint
                    {
                        Label = $"{g.Key.Year}-W{g.Key.Week:D2}",
                        Value = g.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                    .ToList();

                result = grouped;
            }

            return result;
        }









        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramFromTransactions(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();


            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIds = laundromats.Select(l => l.kId).ToList();

            var transactions = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();

            var result = laundromats
                .SelectMany(l =>
                {
                    var ts = transactions.Where(t => t.LaundromatId == l.kId).ToList();
                    var soaps = SoapCount(ts);

                    return new List<ChartDataPoint>
                    {
                        new ChartDataPoint { Label = $"{l.name} - Soap 1", Value = soaps.soap1 },
                        new ChartDataPoint { Label = $"{l.name} - Soap 2", Value = soaps.soap2 },
                        new ChartDataPoint { Label = $"{l.name} - Soap 3", Value = soaps.soap3 },
                    };
                })
                .ToList();

            return result;
        }


        public async Task<List<ChartDataPoint>> CalculateAvgSecoundsFromTransactions(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();


            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
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