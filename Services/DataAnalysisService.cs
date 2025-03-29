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


        
        public decimal CalculateTotalSoapProgramFromTransactions(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t =>(Convert.ToDecimal(t.soap)));
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

        public class ChartDataPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
        }

        public async Task<List<ChartDataPoint>> GetRevenueForAllLaundromatsInBank(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            Console.WriteLine("GetRevenueForAllLaundromatsInBank called with bankId: " + bankId);

            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            Console.WriteLine("Laundromats:");
            Console.WriteLine(JsonConvert.SerializeObject(laundromats, Formatting.Indented));

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
                        Value = ts.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100
                    })
                .ToList();

            return result;
        }
        
        
        
        
        
        
        
        
        

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramFromTransactions(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            Console.WriteLine("GetRevenueForAllLaundromatsInBank called with bankId: " + bankId);
            
            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            Console.WriteLine("Laundromats:");
            Console.WriteLine(JsonConvert.SerializeObject(laundromats, Formatting.Indented));
            
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

            Console.WriteLine("GetRevenueForAllLaundromatsInBank called with bankId: " + bankId);

            var laundromats = await dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            Console.WriteLine("Laundromats:");
            Console.WriteLine(JsonConvert.SerializeObject(laundromats, Formatting.Indented));

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