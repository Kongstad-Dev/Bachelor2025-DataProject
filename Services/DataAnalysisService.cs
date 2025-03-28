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

        public decimal CalculateTotalRevenueForBank(int bankId)
        {
            return _dbContext.Transactions
                .Where(t => _dbContext.Laundromat
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
            var laundromats = await _dbContext.Laundromat
                .AsNoTracking()
                .Where(l => l.bId == bankId)
                .Select(l => new { l.kId, l.name })
                .ToListAsync();


            var results = new List<ChartDataPoint>();

            // Then, perform a separate query per laundromat
            foreach (var l in laundromats)
            {
                var revenue = await _dbContext.Transactions
                    .Where(t => t.LaundromatId == l.kId)
                    .SumAsync(t => Math.Abs(Convert.ToDecimal(t.amount))) / 100;

                results.Add(new ChartDataPoint
                {
                    Label = l.name ?? $"ID {l.kId}",
                    Value = revenue
                });
            }

            return results;
        }



    }
}