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
        ) : base(dbContextFactory, cache)
        {
        }

        public decimal CalculateAvgSecoundsFromTransactions(List<TransactionEntity> transactions)
        {
            var filtered = transactions.Where(t => t.seconds > 0).ToList();

            if (!filtered.Any())
                return 0;

            return filtered.Average(t => Convert.ToDecimal(t.seconds)) / 60; // return in minutes
        }

        public async Task<List<ChartDataPoint>> CalculateTransactionOverTime(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => laundromatIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            var laundromatIdList = laundromats.Select(l => l.kId).ToList();

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
                    .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Count());

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
                    .ToDictionary(g => g.Key, g => g.Count());

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