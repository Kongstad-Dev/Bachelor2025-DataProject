using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using BlazorTest.Database;

namespace BlazorTest.Services
{
    public class DataAnalysisService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;

        public DataAnalysisService(IDbContextFactory<YourDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
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

            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIdList = laundromats.Select(l => l.kId).ToList();

            var transactions = await dbContext.Transactions
                .Where(t => laundromatIdList.Contains(t.LaundromatId) &&
                       t.date >= startDate &&
                       t.date <= endDate)
                .ToListAsync();

            var filtersedTransactions = transactions.Where(t => t.seconds != 0).ToList();

            //Get Total revenue
            var totalRevenue = CalculateLaundromatsRevenue(filtersedTransactions);
            //Get average revenue
            var avgRevenue = filtersedTransactions.Count > 0 ? totalRevenue / filtersedTransactions.Count : 0;
            //get Total transactions
            var totalTransactions = filtersedTransactions.Count;
            //Get average transactions
            var avgTransactions = filtersedTransactions.Count > 0 ? totalTransactions / laundromatIds.Count : 0;

            //Get washing machine percentage
            var dryerIDs = new int[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };
            var dryerTransactions = filtersedTransactions.Where(t => dryerIDs.Contains(t.unitType)).ToList();
            var dryerPercentage = totalTransactions > 0 ? (decimal)dryerTransactions.Count / totalTransactions * 100 : 0;
            var washingPercentage = 100 - dryerPercentage;

            //Return the result with names
            var result = new List<KeyValuePair<string, decimal>>
    {
        new KeyValuePair<string, decimal>("Total Revenue", totalRevenue),
        new KeyValuePair<string, decimal>("Average Revenue", avgRevenue),
        new KeyValuePair<string, decimal>("Total Transactions", totalTransactions),
        new KeyValuePair<string, decimal>("Average Transactions", avgTransactions),
        new KeyValuePair<string, decimal>("Washing Machine %", washingPercentage),
        new KeyValuePair<string, decimal>("Dryer %", dryerPercentage)
    };

            return result;
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

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromats(List<string> laundromatIds, DateTime? startDate, DateTime? endDate)
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
                       t.date <= endDate)
                .ToListAsync();

            // Group and compute revenue per laundromat
            var result = laundromats
                .GroupJoin(transactions,
                    l => l.kId,
                    t => t.LaundromatId,
                    (l, ts) => new ChartDataPoint
                    {
                        Label = l.name ?? $"ID {l.kId}",
                        Value = ts.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                .ToList();

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