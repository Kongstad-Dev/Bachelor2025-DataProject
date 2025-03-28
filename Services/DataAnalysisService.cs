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

        public decimal CalculateRevenueFromTransactions(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100;
        }

        public decimal CalculateTotalRevenueForBank(int bankId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return dbContext.Transactions
                .Where(t => dbContext.Laundromat
                    .Any(l => l.bId == bankId && l.kId == t.LaundromatId))
                .Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100;
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

            var results = new List<ChartDataPoint>();

            var laundromatIds = laundromats.Select(l => l.kId).ToList();
            
            var transactions = await dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();
            //Hej
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



    }
}