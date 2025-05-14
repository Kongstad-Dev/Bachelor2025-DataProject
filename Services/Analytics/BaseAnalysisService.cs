using BlazorTest.Database.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public abstract class BaseAnalysisService
    {
        protected readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        protected readonly IMemoryCache _cache;

        public BaseAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        )
        {
            _dbContextFactory = dbContextFactory;
            _cache = cache;
        }

        protected bool DateEquals(DateTime date1, DateTime date2)
        {
            return date1.Date == date2.Date;
        }

        protected StatsPeriodType? GetMatchingStatsPeriodType(
            DateTime? startDate,
            DateTime? endDate
        )
        {
            if (startDate == null || endDate == null)
                return null;

            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000
            var oneMonthAgo = startOfToday.AddMonths(-1);
            var sixMonthsAgo = startOfToday.AddMonths(-6);
            var yearAgo = startOfToday.AddYears(-1);

            // CASE 1: Check for Month period match
            if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, oneMonthAgo))
            {
                return StatsPeriodType.Month;
            }
            // CASE 2: Check for HalfYear period match
            else if (
                DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, sixMonthsAgo)
            )
            {
                return StatsPeriodType.HalfYear;
            }
            // CASE 3: Check for Year period match
            else if (DateEquals(endDate.Value, endOfToday) && DateEquals(startDate.Value, yearAgo))
            {
                return StatsPeriodType.Year;
            }
            // CASE 4: Check for Quarter matches
            else
            {
                // Calculate the current quarter details
                int currentQuarter = (now.Month + 2) / 3;
                int currentYear = now.Year;

                // Check current and previous quarters
                for (int i = 0; i < 4; i++)
                {
                    int offset = i;
                    int quarter = currentQuarter - (offset % 4);
                    int yearOffset = offset / 4;

                    if (quarter <= 0)
                    {
                        quarter += 4;
                        yearOffset++;
                    }

                    int year = currentYear - yearOffset;

                    // Calculate quarter start and end dates
                    int startMonth = (quarter - 1) * 3 + 1;
                    var quarterStartDate = new DateTime(year, startMonth, 1);
                    var quarterEndDate = quarterStartDate
                        .AddMonths(3)
                        .AddDays(-1)
                        .Date.AddDays(1)
                        .AddMilliseconds(-1);

                    // Check if exact match (date only)
                    if (
                        DateEquals(startDate.Value, quarterStartDate)
                        && DateEquals(endDate.Value, quarterEndDate)
                    )
                    {
                        return StatsPeriodType.Quarter;
                    }
                }

                // CASE 5: Check for Past 4 Completed Quarters match
                // Calculate the start and end dates for the past 4 completed quarters
                int lastCompletedQuarter = currentQuarter - 1;
                int lastCompletedYear = currentYear;

                if (lastCompletedQuarter <= 0)
                {
                    lastCompletedQuarter += 4;
                    lastCompletedYear -= 1;
                }

                // Calculate end date (last day of the last completed quarter)
                int lastQuarterLastMonth = lastCompletedQuarter * 3;
                var completedQuartersEndDate = new DateTime(
                    lastCompletedYear,
                    lastQuarterLastMonth,
                    1
                )
                    .AddMonths(1)
                    .AddDays(-1);

                // Calculate start date (first day 4 quarters back from the last completed quarter)
                int startQuarter = lastCompletedQuarter - 3;
                int startYear = lastCompletedYear;

                if (startQuarter <= 0)
                {
                    startQuarter += 4;
                    startYear -= 1;
                }

                int completedQuartersStartMonth = (startQuarter - 1) * 3 + 1;
                var completedQuartersStartDate = new DateTime(
                    startYear,
                    completedQuartersStartMonth,
                    1
                );
                // Check if exact match for past 4 completed quarters
                if (
                    DateEquals(startDate.Value, completedQuartersStartDate)
                    && DateEquals(endDate.Value, completedQuartersEndDate)
                )
                {
                    return StatsPeriodType.CompletedQuarters;
                }
            }

            return null;
        }

        protected string GetQuarterPeriodKey(DateTime? startDate)
        {
            if (startDate == null)
                return null;

            var year = startDate.Value.Year;
            var quarter = (startDate.Value.Month + 2) / 3;
            return $"{year}-Q{quarter}";
        }

        protected string GetPeriodName(StatsPeriodType periodType, string periodKey = null)
        {
            return periodType switch
            {
                StatsPeriodType.Month => "Last Month",
                StatsPeriodType.HalfYear => "Last 6 Months",
                StatsPeriodType.Year => "Last Year",
                StatsPeriodType.Quarter when periodKey != null => periodKey.Replace("-Q", " Q"),
                StatsPeriodType.CompletedQuarters => "Past 4 Completed Quarters",
                _ => "Custom",
            };
        }

        protected string GetPeriodKeyForStats(StatsPeriodType periodType, DateTime date)
        {
            switch (periodType)
            {
                case StatsPeriodType.Month:
                    return "last-month";
                case StatsPeriodType.HalfYear:
                    return "last-6-months";
                case StatsPeriodType.Year:
                    return "last-year";
                case StatsPeriodType.Quarter:
                    return GetQuarterPeriodKey(date);
                case StatsPeriodType.CompletedQuarters:
                    return "past-4-completed-quarters";
                default:
                    return null;
            }
        }
    }
}
