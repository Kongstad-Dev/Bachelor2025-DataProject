using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using BlazorTest.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorTest.Services
{
    public class LaundromatStatsService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly ILogger<LaundromatStatsService> _logger;

        public LaundromatStatsService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            ILogger<LaundromatStatsService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<List<LaundromatStats>> GetLaundromatStatsAsync(
            List<string> laundromatIds, 
            DateTime? startDate = null, 
            DateTime? endDate = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.LaundromatStats.AsNoTracking();

            if (laundromatIds != null && laundromatIds.Any())
            {
                query = query.Where(s => laundromatIds.Contains(s.LaundromatId));
            }

            if (startDate != null)
            {
                query = query.Where(s => s.StartDate >= startDate);
            }

            if (endDate != null)
            {
                query = query.Where(s => s.EndDate <= endDate);
            }

            return await query.ToListAsync();
        }

        public async Task<LaundromatStats> GetLaundromatStatsByIdAsync(
            string laundromatId, 
            DateTime date)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.LaundromatStats.AsNoTracking()
                .FirstOrDefaultAsync(s => s.LaundromatId == laundromatId && 
                                         (s.StartDate <= date && s.EndDate >= date));
        }

        public async Task<List<LaundromatStats>> GetAggregatedLaundromatStatsAsync(
            List<string> laundromatIds,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.LaundromatStats.AsNoTracking();

            if (laundromatIds != null && laundromatIds.Any())
            {
                query = query.Where(s => laundromatIds.Contains(s.LaundromatId));
            }

            if (startDate != null)
            {
                query = query.Where(s => s.StartDate >= startDate);
            }

            if (endDate != null)
            {
                query = query.Where(s => s.EndDate <= endDate);
            }

            // Group by laundromat and aggregate stats
            var result = await query
                .GroupBy(s => s.LaundromatId)
                .Select(g => new LaundromatStats
                {
                    LaundromatId = g.Key,
                    LaundromatName = g.First().LaundromatName,
                    TotalRevenue = g.Sum(s => s.TotalRevenue),
                    TotalTransactions = g.Sum(s => s.TotalTransactions),
                    DryerTransactions = g.Sum(s => s.DryerTransactions),
                    WashingMachineTransactions = g.Sum(s => s.WashingMachineTransactions),
                    StartDate = startDate ?? g.Min(s => s.StartDate),
                    EndDate = endDate ?? g.Max(s => s.EndDate),
                    CalculatedAt = DateTime.Now
                })
                .ToListAsync();

            return result;
        }
        
        public async Task UpdateStatsForLaundromatAsync(string laundromatId)
        {
            if (string.IsNullOrEmpty(laundromatId))
            {
                throw new ArgumentException("Laundromat ID is required", nameof(laundromatId));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                bool exists = await dbContext.Laundromat.AnyAsync(l => l.kId == laundromatId);
                if (!exists)
                {
                    throw new KeyNotFoundException($"Laundromat with ID {laundromatId} not found");
                }

                await CalculateStatsForLaundromat(laundromatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating stats for laundromat {laundromatId}");
                throw;
            }
        }

        public async Task UpdateAllStatsAsync()
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var laundromatIds = await dbContext.Laundromat.Select(l => l.kId).ToListAsync();
                int count = 0;

                foreach (var id in laundromatIds)
                {
                    await CalculateStatsForLaundromat(id);
                    count++;
                }

                _logger.LogInformation($"Stats updated for {count} laundromats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stats for all laundromats");
                throw;
            }
        }

        private async Task CalculateStatsForLaundromat(string laundromatId)
        {
            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000

            // Use a single context for all operations
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Get the laundromat name once for all operations
            var laundromat = await dbContext.Laundromat.FindAsync(laundromatId);
            if (laundromat == null)
            {
                _logger.LogWarning($"Laundromat with ID {laundromatId} not found");
                return;
            }

            string laundromatName = laundromat.name ?? "Unknown Laundromat"; // Ensure we have a name

            // 1. Calculate Month stats
            var thirtyDaysAgo = startOfToday.AddMonths(-1);
            await CalculateStats(
                dbContext,
                laundromatId,
                laundromatName,
                StatsPeriodType.Month,
                "last-month",
                thirtyDaysAgo,
                endOfToday
            );

            // 2. Calculate Half Year stats
            var sixMonthsAgo = startOfToday.AddMonths(-6);
            await CalculateStats(
                dbContext,
                laundromatId,
                laundromatName,
                StatsPeriodType.HalfYear,
                "last-6-months",
                sixMonthsAgo,
                endOfToday
            );

            // 3. Calculate Year stats
            var yearAgo = startOfToday.AddYears(-1);
            await CalculateStats(
                dbContext,
                laundromatId,
                laundromatName,
                StatsPeriodType.Year,
                "last-year",
                yearAgo,
                endOfToday
            );

            // 4. Calculate quarterly stats (last 4 quarters)
            int currentQuarter = (now.Month + 2) / 3;
            int currentYear = now.Year;

            for (int i = 0; i < 4; i++)
            {
                int offsetQuarters = i;
                int quarter = currentQuarter - (offsetQuarters % 4);
                int yearOffset = offsetQuarters / 4;

                if (quarter <= 0)
                {
                    quarter += 4;
                    yearOffset++;
                }

                int year = currentYear - yearOffset;
                string periodKey = $"{year}-Q{quarter}";

                await CalculateQuarterlyStats(
                    dbContext,
                    laundromatId,
                    laundromatName,
                    year,
                    quarter,
                    periodKey
                );
            }

            // 5. Calculate stats for past 4 completed quarters combined (excluding current quarter)
            await CalculatePast4CompletedQuartersStats(
                dbContext,
                laundromatId,
                laundromatName,
                currentYear,
                currentQuarter
            );

            // Save all changes at once
            await dbContext.SaveChangesAsync();
        }

        private async Task CalculateStats(
            YourDbContext dbContext,
            string laundromatId,
            string laundromatName,
            StatsPeriodType periodType,
            string periodKey,
            DateTime startDate,
            DateTime endDate
        )
        {
            // Check if stats for this period already exist
            var existingStats = await dbContext.LaundromatStats.FirstOrDefaultAsync(s =>
                s.LaundromatId == laundromatId && s.PeriodType == periodType
            );

            // Define dryer unit types
            var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

            // Calculate stats from transactions with additional revenue data
            var stats =
                await dbContext
                    .Transactions.Where(t =>
                        t.LaundromatId == laundromatId && t.date >= startDate && t.date <= endDate
                    )
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        TotalTransactions = g.Count(),
                        TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        DryerTransactions = g.Count(t => dryerUnitTypes.Contains(t.unitType)),
                        DryerRevenue = g.Sum(t =>
                            dryerUnitTypes.Contains(t.unitType) ? Math.Abs(t.amount) / 100m : 0m
                        ),
                    })
                    .FirstOrDefaultAsync()
                ?? new
                {
                    TotalTransactions = 0,
                    TotalRevenue = 0m,
                    DryerTransactions = 0,
                    DryerRevenue = 0m,
                };

            // Calculate washer transactions
            int washerTransactions = stats.TotalTransactions - stats.DryerTransactions;

            // Calculate price per transaction type (avoiding divide by zero)
            decimal dryerStartPrice =
                stats.DryerTransactions > 0 ? stats.DryerRevenue / stats.DryerTransactions : 0;

            decimal washerRevenue = stats.TotalRevenue - stats.DryerRevenue;
            decimal washerStartPrice =
                washerTransactions > 0 ? washerRevenue / washerTransactions : 0;

            // Generate time series data for revenue
            var revenueTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                periodType,
                TimeSeriesDataType.Revenue
            );

            // Generate time series data for transaction counts
            var transactionCountTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                periodType,
                TimeSeriesDataType.TransactionCount
            );

            // Convert to JSON
            string revenueTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                revenueTimeSeriesData
            );

            string transactionCountTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                transactionCountTimeSeriesData
            );

            // Calculate available time series data types
            TimeSeriesDataTypes availableTypes = TimeSeriesDataTypes.None;

            if (!string.IsNullOrEmpty(revenueTimeSeriesJson))
                availableTypes |= TimeSeriesDataTypes.Revenue;

            if (!string.IsNullOrEmpty(transactionCountTimeSeriesJson))
                availableTypes |= TimeSeriesDataTypes.TransactionCount;

            // Create or update stats entity
            if (existingStats == null)
            {
                existingStats = new LaundromatStats
                {
                    LaundromatId = laundromatId,
                    LaundromatName = laundromatName,
                    PeriodType = periodType,
                    PeriodKey = periodKey,
                    StartDate = startDate,
                    EndDate = endDate,
                    WasherStartPrice = washerStartPrice,
                    DryerStartPrice = dryerStartPrice,
                    RevenueTimeSeriesData = revenueTimeSeriesJson,
                    TransactionCountTimeSeriesData = transactionCountTimeSeriesJson,
                    AvailableTimeSeriesData = availableTypes,
                };
                dbContext.LaundromatStats.Add(existingStats);
            }
            else
            {
                // Update date range for rolling periods
                existingStats.StartDate = startDate;
                existingStats.EndDate = endDate;
                existingStats.WasherStartPrice = washerStartPrice;
                existingStats.DryerStartPrice = dryerStartPrice;
                existingStats.RevenueTimeSeriesData = revenueTimeSeriesJson;
                existingStats.TransactionCountTimeSeriesData = transactionCountTimeSeriesJson;
                existingStats.AvailableTimeSeriesData = availableTypes;
                // Make sure LaundromatName is set
                if (string.IsNullOrEmpty(existingStats.LaundromatName))
                {
                    existingStats.LaundromatName = laundromatName;
                }
            }

            // Update stats values
            existingStats.TotalTransactions = stats.TotalTransactions;
            existingStats.TotalRevenue = stats.TotalRevenue;
            existingStats.DryerTransactions = stats.DryerTransactions;
            existingStats.WashingMachineTransactions = washerTransactions;
            existingStats.CalculatedAt = DateTime.Now;
        }

        private async Task CalculateQuarterlyStats(
            YourDbContext dbContext,
            string laundromatId,
            string laundromatName,
            int year,
            int quarter,
            string periodKey
        )
        {
            int startMonth = (quarter - 1) * 3 + 1;
            DateTime startDate = new DateTime(year, startMonth, 1);
            DateTime endDate = startDate.AddMonths(3).AddDays(-1);

            var existingStats = await dbContext.LaundromatStats.FirstOrDefaultAsync(s =>
                s.LaundromatId == laundromatId
                && s.PeriodType == StatsPeriodType.Quarter
                && s.PeriodKey == periodKey
            );

            // Define dryer unit types
            var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

            // Calculate stats from transactions with additional revenue data
            var stats =
                await dbContext
                    .Transactions.Where(t =>
                        t.LaundromatId == laundromatId && t.date >= startDate && t.date <= endDate
                    )
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        TotalTransactions = g.Count(),
                        TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        DryerTransactions = g.Count(t => dryerUnitTypes.Contains(t.unitType)),
                        DryerRevenue = g.Sum(t =>
                            dryerUnitTypes.Contains(t.unitType) ? Math.Abs(t.amount) / 100m : 0m
                        ),
                    })
                    .FirstOrDefaultAsync()
                ?? new
                {
                    TotalTransactions = 0,
                    TotalRevenue = 0m,
                    DryerTransactions = 0,
                    DryerRevenue = 0m,
                };

            // Calculate washer transactions
            int washerTransactions = stats.TotalTransactions - stats.DryerTransactions;

            // Calculate price per transaction type (avoiding divide by zero)
            decimal dryerStartPrice =
                stats.DryerTransactions > 0 ? stats.DryerRevenue / stats.DryerTransactions : 0;

            decimal washerRevenue = stats.TotalRevenue - stats.DryerRevenue;
            decimal washerStartPrice =
                washerTransactions > 0 ? washerRevenue / washerTransactions : 0;

            // Generate time series data for revenue
            var revenueTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                StatsPeriodType.Quarter,
                TimeSeriesDataType.Revenue
            );

            // Generate time series data for transaction counts
            var transactionCountTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                StatsPeriodType.Quarter,
                TimeSeriesDataType.TransactionCount
            );

            // Convert to JSON - ALWAYS provide a valid JSON, never null
            string revenueTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                revenueTimeSeriesData
            );
            string transactionCountTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                transactionCountTimeSeriesData
            );

            // Calculate available time series data types
            TimeSeriesDataTypes availableTypes = TimeSeriesDataTypes.None;

            if (revenueTimeSeriesData.DataPoints?.Count > 0)
                availableTypes |= TimeSeriesDataTypes.Revenue;

            if (transactionCountTimeSeriesData.DataPoints?.Count > 0)
                availableTypes |= TimeSeriesDataTypes.TransactionCount;

            // Create or update stats entity
            if (existingStats == null)
            {
                existingStats = new LaundromatStats
                {
                    LaundromatId = laundromatId,
                    LaundromatName = laundromatName,
                    PeriodType = StatsPeriodType.Quarter,
                    PeriodKey = periodKey,
                    StartDate = startDate,
                    EndDate = endDate,
                    WasherStartPrice = washerStartPrice,
                    DryerStartPrice = dryerStartPrice,
                    RevenueTimeSeriesData = revenueTimeSeriesJson,
                    TransactionCountTimeSeriesData = transactionCountTimeSeriesJson,
                    AvailableTimeSeriesData = availableTypes,
                };
                dbContext.LaundromatStats.Add(existingStats);
            }
            else
            {
                // Make sure LaundromatName is set
                if (string.IsNullOrEmpty(existingStats.LaundromatName))
                {
                    existingStats.LaundromatName = laundromatName;
                }
                existingStats.RevenueTimeSeriesData = revenueTimeSeriesJson;
                existingStats.TransactionCountTimeSeriesData = transactionCountTimeSeriesJson;
                existingStats.AvailableTimeSeriesData = availableTypes;
                existingStats.WasherStartPrice = washerStartPrice;
                existingStats.DryerStartPrice = dryerStartPrice;
            }

            // Update stats values
            existingStats.TotalTransactions = stats.TotalTransactions;
            existingStats.TotalRevenue = stats.TotalRevenue;
            existingStats.DryerTransactions = stats.DryerTransactions;
            existingStats.WashingMachineTransactions = washerTransactions;
            existingStats.CalculatedAt = DateTime.Now;
        }

        private async Task CalculatePast4CompletedQuartersStats(
            YourDbContext dbContext,
            string laundromatId,
            string laundromatName,
            int currentYear,
            int currentQuarter
        )
        {
            // Calculate the start and end dates for the past 4 completed quarters
            // We need to move back one quarter from the current quarter to get to the last completed quarter
            int lastCompletedQuarter = currentQuarter - 1;
            int lastCompletedYear = currentYear;

            if (lastCompletedQuarter <= 0)
            {
                lastCompletedQuarter += 4;
                lastCompletedYear -= 1;
            }

            // Calculate end date (last day of the last completed quarter)
            int lastQuarterLastMonth = lastCompletedQuarter * 3;
            DateTime endDate = new DateTime(lastCompletedYear, lastQuarterLastMonth, 1)
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

            int startMonth = (startQuarter - 1) * 3 + 1;
            DateTime startDate = new DateTime(startYear, startMonth, 1);

            string periodKey = "past-4-completed-quarters";

            // Check if stats for this period already exist
            var existingStats = await dbContext.LaundromatStats.FirstOrDefaultAsync(s =>
                s.LaundromatId == laundromatId
                && s.PeriodType == StatsPeriodType.CompletedQuarters
                && s.PeriodKey == periodKey
            );

            // Define dryer unit types
            var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

            // Calculate stats from transactions
            var stats =
                await dbContext
                    .Transactions.Where(t =>
                        t.LaundromatId == laundromatId && t.date >= startDate && t.date <= endDate
                    )
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        TotalTransactions = g.Count(),
                        TotalRevenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        DryerTransactions = g.Count(t => dryerUnitTypes.Contains(t.unitType)),
                        DryerRevenue = g.Sum(t =>
                            dryerUnitTypes.Contains(t.unitType) ? Math.Abs(t.amount) / 100m : 0m
                        ),
                    })
                    .FirstOrDefaultAsync()
                ?? new
                {
                    TotalTransactions = 0,
                    TotalRevenue = 0m,
                    DryerTransactions = 0,
                    DryerRevenue = 0m,
                };

            // Calculate washer transactions
            int washerTransactions = stats.TotalTransactions - stats.DryerTransactions;

            // Calculate price per transaction type (avoiding divide by zero)
            decimal dryerStartPrice =
                stats.DryerTransactions > 0 ? stats.DryerRevenue / stats.DryerTransactions : 0;

            decimal washerRevenue = stats.TotalRevenue - stats.DryerRevenue;
            decimal washerStartPrice =
                washerTransactions > 0 ? washerRevenue / washerTransactions : 0;

            // Generate time series data for revenue
            var revenueTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                StatsPeriodType.CompletedQuarters,
                TimeSeriesDataType.Revenue
            );

            // Generate time series data for transaction counts
            var transactionCountTimeSeriesData = await GenerateTimeSeriesData(
                dbContext,
                laundromatId,
                startDate,
                endDate,
                StatsPeriodType.CompletedQuarters,
                TimeSeriesDataType.TransactionCount
            );

            // Convert to JSON - ALWAYS provide a valid JSON, never null
            string revenueTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                revenueTimeSeriesData
            );
            string transactionCountTimeSeriesJson = System.Text.Json.JsonSerializer.Serialize(
                transactionCountTimeSeriesData
            );

            // Calculate available time series data types
            TimeSeriesDataTypes availableTypes = TimeSeriesDataTypes.None;

            if (revenueTimeSeriesData.DataPoints?.Count > 0)
                availableTypes |= TimeSeriesDataTypes.Revenue;

            if (transactionCountTimeSeriesData.DataPoints?.Count > 0)
                availableTypes |= TimeSeriesDataTypes.TransactionCount;

            // Create or update stats entity
            if (existingStats == null)
            {
                existingStats = new LaundromatStats
                {
                    LaundromatId = laundromatId,
                    LaundromatName = laundromatName,
                    PeriodType = StatsPeriodType.CompletedQuarters,
                    PeriodKey = periodKey,
                    StartDate = startDate,
                    EndDate = endDate,
                    WasherStartPrice = washerStartPrice,
                    DryerStartPrice = dryerStartPrice,
                    RevenueTimeSeriesData = revenueTimeSeriesJson,
                    TransactionCountTimeSeriesData = transactionCountTimeSeriesJson,
                    AvailableTimeSeriesData = availableTypes,
                };
                dbContext.LaundromatStats.Add(existingStats);
            }
            else
            {
                // Update date range
                existingStats.RevenueTimeSeriesData = revenueTimeSeriesJson;
                existingStats.TransactionCountTimeSeriesData = transactionCountTimeSeriesJson;
                existingStats.AvailableTimeSeriesData = availableTypes;
                existingStats.StartDate = startDate;
                existingStats.EndDate = endDate;
                existingStats.WasherStartPrice = washerStartPrice;
                existingStats.DryerStartPrice = dryerStartPrice;

                // Make sure LaundromatName is set
                if (string.IsNullOrEmpty(existingStats.LaundromatName))
                {
                    existingStats.LaundromatName = laundromatName;
                }
            }

            // Update stats values
            existingStats.TotalTransactions = stats.TotalTransactions;
            existingStats.TotalRevenue = stats.TotalRevenue;
            existingStats.DryerTransactions = stats.DryerTransactions;
            existingStats.WashingMachineTransactions = washerTransactions;
            existingStats.CalculatedAt = DateTime.Now;
        }

        private async Task<TimeSeriesInfo> GenerateTimeSeriesData(
            YourDbContext dbContext,
            string laundromatId,
            DateTime startDate,
            DateTime endDate,
            StatsPeriodType periodType,
            TimeSeriesDataType dataType
        )
        {
            // Determine interval based on period type
            string interval;
            switch (periodType)
            {
                case StatsPeriodType.Month:
                    interval = "day"; // Daily data points for month view
                    break;
                case StatsPeriodType.HalfYear:
                    interval = "week"; // Weekly data points for half-year view
                    break;
                case StatsPeriodType.Year:
                case StatsPeriodType.CompletedQuarters:
                    interval = "month"; // Monthly data points for year view
                    break;
                case StatsPeriodType.Quarter:
                    interval = startDate.AddDays(90) < endDate ? "month" : "week"; // Use weeks for short quarters, months for longer periods
                    break;
                default:
                    return new TimeSeriesInfo
                    {
                        DataPoints = new List<ChartDataPoint>(),
                        Interval = "unknown",
                        StartDate = startDate,
                        EndDate = endDate,
                    };
            }

            var result = new List<ChartDataPoint>();
            var totalDays = (endDate - startDate).TotalDays;

            if (interval == "day")
            {
                // Generate all days between startDate and endDate
                var allDays = Enumerable
                    .Range(0, (int)totalDays + 1)
                    .Select(i => startDate.AddDays(i).Date)
                    .ToList();

                if (dataType == TimeSeriesDataType.Revenue)
                {
                    // Get daily revenue data
                    var dailyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                            && t.amount != 0
                        )
                        .GroupBy(t => t.date.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Value = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        })
                        .ToDictionaryAsync(k => k.Date, v => v.Value);

                    // Create data points for all days
                    result = allDays
                        .Select(day => new ChartDataPoint
                        {
                            Label = day.ToString("yyyy-MM-dd"),
                            Value = dailyData.ContainsKey(day) ? dailyData[day] : 0m,
                        })
                        .ToList();
                }
                else if (dataType == TimeSeriesDataType.TransactionCount)
                {
                    // Get daily transaction counts
                    var dailyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                        )
                        .GroupBy(t => t.date.Date)
                        .Select(g => new { Date = g.Key, Value = g.Count() })
                        .ToDictionaryAsync(k => k.Date, v => v.Value);

                    // Create data points for all days
                    result = allDays
                        .Select(day => new ChartDataPoint
                        {
                            Label = day.ToString("yyyy-MM-dd"),
                            Value = dailyData.ContainsKey(day) ? dailyData[day] : 0,
                        })
                        .ToList();
                }
            }
            else if (interval == "week")
            {
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                int totalWeeks = (int)(totalDays / 7) + 1;

                // Generate all weeks between startDate and endDate
                var allWeeks = Enumerable
                    .Range(0, totalWeeks)
                    .Select(i => startDate.AddDays(i * 7))
                    .Select(d => new
                    {
                        Date = d,
                        Year = d.Year,
                        Week = calendar.GetWeekOfYear(
                            d,
                            System.Globalization.CalendarWeekRule.FirstDay,
                            DayOfWeek.Monday
                        ),
                    })
                    .ToList();

                if (dataType == TimeSeriesDataType.Revenue)
                {
                    // Get daily data first (this can be done in the database)
                    var dailyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                            && t.amount != 0
                        )
                        // Pull only necessary data to client side
                        .Select(t => new { t.date, t.amount })
                        .AsNoTracking()
                        .ToListAsync();

                    // Then do the week grouping on the client side
                    var weeklyData = dailyData
                        .GroupBy(t => new
                        {
                            Year = t.date.Year,
                            Week = calendar.GetWeekOfYear(
                                t.date,
                                System.Globalization.CalendarWeekRule.FirstDay,
                                DayOfWeek.Monday
                            ),
                        })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Week,
                            Value = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        })
                        .ToDictionary(k => new { k.Year, k.Week }, v => v.Value);

                    // Create data points for all weeks
                    result = allWeeks
                        .Select(w =>
                        {
                            var key = new { w.Year, w.Week };
                            return new ChartDataPoint
                            {
                                Label = $"{w.Year}-W{w.Week:D2}",
                                Value = weeklyData.ContainsKey(key) ? weeklyData[key] : 0m,
                            };
                        })
                        .ToList();
                }
                else if (dataType == TimeSeriesDataType.TransactionCount)
                {
                    // Get daily data first (this can be done in the database)
                    var dailyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                        )
                        // Select the transaction entity or at least a reference type, not just the date
                        .Select(t => new { t.date }) // Change this from just t.date to an anonymous object
                        .AsNoTracking()
                        .ToListAsync();

                    // Then do the week grouping on the client side
                    var weeklyData = dailyData
                        .GroupBy(t => new
                        {
                            Year = t.date.Year,
                            Week = calendar.GetWeekOfYear(
                                t.date,
                                System.Globalization.CalendarWeekRule.FirstDay,
                                DayOfWeek.Monday
                            ),
                        })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Week,
                            Value = g.Count(),
                        })
                        .ToDictionary(k => new { k.Year, k.Week }, v => v.Value);

                    // Create data points for all weeks
                    result = allWeeks
                        .Select(w =>
                        {
                            var key = new { w.Year, w.Week };
                            return new ChartDataPoint
                            {
                                Label = $"{w.Year}-W{w.Week:D2}",
                                Value = weeklyData.ContainsKey(key) ? weeklyData[key] : 0,
                            };
                        })
                        .ToList();
                }
            }
            else if (interval == "month")
            {
                // Calculate total months
                int totalMonths =
                    (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1;

                // Generate all months between startDate and endDate
                var allMonths = Enumerable
                    .Range(0, totalMonths)
                    .Select(i => startDate.AddMonths(i))
                    .Select(d => new { Year = d.Year, Month = d.Month })
                    .ToList();

                if (dataType == TimeSeriesDataType.Revenue)
                {
                    // Get monthly revenue data
                    var monthlyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                            && t.amount != 0
                        )
                        .GroupBy(t => new { t.date.Year, t.date.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            Value = g.Sum(t => Math.Abs(t.amount)) / 100m,
                        })
                        .ToDictionaryAsync(k => new { k.Year, k.Month }, v => v.Value);

                    // Create data points for all months
                    result = allMonths
                        .Select(m =>
                        {
                            var key = new { m.Year, m.Month };
                            return new ChartDataPoint
                            {
                                Label = $"{m.Year}-{m.Month:D2}",
                                Value = monthlyData.ContainsKey(key) ? monthlyData[key] : 0m,
                            };
                        })
                        .ToList();
                }
                else if (dataType == TimeSeriesDataType.TransactionCount)
                {
                    // Get monthly transaction counts
                    var monthlyData = await dbContext
                        .Transactions.Where(t =>
                            t.LaundromatId == laundromatId
                            && t.date >= startDate
                            && t.date <= endDate
                        )
                        .GroupBy(t => new { t.date.Year, t.date.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            Value = g.Count(),
                        })
                        .ToDictionaryAsync(k => new { k.Year, k.Month }, v => v.Value);

                    // Create data points for all months
                    result = allMonths
                        .Select(m =>
                        {
                            var key = new { m.Year, m.Month };
                            return new ChartDataPoint
                            {
                                Label = $"{m.Year}-{m.Month:D2}",
                                Value = monthlyData.ContainsKey(key) ? monthlyData[key] : 0,
                            };
                        })
                        .ToList();
                }
            }

            return new TimeSeriesInfo
            {
                DataPoints = result,
                Interval = interval,
                StartDate = startDate,
                EndDate = endDate,
            };
        }

        public enum TimeSeriesDataType
        {
            Revenue,
            TransactionCount,
        }
    }
}