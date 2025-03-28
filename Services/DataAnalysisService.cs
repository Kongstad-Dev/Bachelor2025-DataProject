using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;

namespace BlazorTest.Services
{
    public class DataAnalysisService
    {
        private readonly YourDbContext _dbContext;

        public DataAnalysisService(YourDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public decimal CalculateRevenueFromTransactions(List<TransactionEntity> transactions)
        {
            return transactions.Sum(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100;
        }


        public class ChartDataPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
        }
        public async Task<List<ChartDataPoint>> GetRevenueForAllLaundromatsInBank(int bankId)
        {
            // Query 1: Get all laundromats under the bank
            var laundromats = await _dbContext.Laundromat
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIds = laundromats.Select(l => l.kId).ToList();

            // Query 2: Get all relevant transactions
            var transactions = await _dbContext.Transactions
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