using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class SoapAnalysisService : BaseAnalysisService
    {
        public SoapAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public decimal CalculateTotalSoapProgramFromTransactions(
            List<TransactionEntity> transactions
        )
        {
            return transactions.Sum(t => (Convert.ToDecimal(t.soap)));
        }

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var transactions = await dbContext
                .Transactions.AsNoTracking()
                .Where(t =>
                    laundromatIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                )
                .ToListAsync();

            int soap1Count = transactions.Count(t => t.soap == 1);
            int soap2Count = transactions.Count(t => t.soap == 2);
            int soap3Count = transactions.Count(t => t.soap == 3);

            return new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Soap 1", Value = soap1Count },
                new ChartDataPoint { Label = "Soap 2", Value = soap2Count },
                new ChartDataPoint { Label = "Soap 3", Value = soap3Count },
            };
        }

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var transactions = await dbContext
                .Transactions.Where(t =>
                    laundromatIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.soap > 0
                ) // only valid soap usages
                .ToListAsync();

            int total = transactions.Count;

            int soap1Count = transactions.Count(t => t.soap == 1);
            int soap2Count = transactions.Count(t => t.soap == 2);
            int soap3Count = transactions.Count(t => t.soap == 3);

            decimal soap1Percent =
                total == 0 ? 0 : Math.Round((decimal)soap1Count / total * 100, 2);
            decimal soap2Percent =
                total == 0 ? 0 : Math.Round((decimal)soap2Count / total * 100, 2);
            decimal soap3Percent =
                total == 0 ? 0 : Math.Round(100 - soap1Percent - soap2Percent, 2); // adjust last one

            return new List<ChartDataPoint>
            {
                new ChartDataPoint { Label = "Soap 1", Value = soap1Percent },
                new ChartDataPoint { Label = "Soap 2", Value = soap2Percent },
                new ChartDataPoint { Label = "Soap 3", Value = soap3Percent },
            };
        }
    }
}