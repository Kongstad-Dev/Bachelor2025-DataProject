using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorTest.Database.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LaundromatStatsController : ControllerBase
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly ILogger<LaundromatStatsController> _logger;

        public LaundromatStatsController(
            IDbContextFactory<YourDbContext> dbContextFactory,
            ILogger<LaundromatStatsController> logger
        )
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        [HttpPost("update/{laundromatId}")]
        public async Task<IActionResult> UpdateStats(string laundromatId)
        {
            if (string.IsNullOrEmpty(laundromatId))
            {
                return BadRequest("Laundromat ID is required");
            }

            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                bool exists = await dbContext.Laundromat.AnyAsync(l => l.kId == laundromatId);
                if (!exists)
                {
                    return NotFound($"Laundromat with ID {laundromatId} not found");
                }

                await CalculateStatsForLaundromat(laundromatId);
                return Ok(new { message = $"Stats updated for laundromat {laundromatId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating stats for laundromat {laundromatId}");
                return StatusCode(500, $"Error updating stats: {ex.Message}");
            }
        }

        [HttpPost("update-all")]
        public async Task<IActionResult> UpdateAllStats()
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var laundromatIds = await dbContext.Laundromat.Select(l => l.kId).ToListAsync();
                int count = 0;

                foreach (var id in laundromatIds)
                {
                    await CalculateStatsForLaundromat(id);
                    count++;
                }

                return Ok(new { message = $"Stats updated for {count} laundromats" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stats for all laundromats");
                return StatusCode(500, $"Error updating stats: {ex.Message}");
            }
        }

        private async Task CalculateStatsForLaundromat(string laundromatId)
        {
            var now = DateTime.Now;
            var endOfToday = now.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999
            var startOfToday = now.Date; // 00:00:00.000

            // Use a single context for all operations
            using var dbContext = _dbContextFactory.CreateDbContext();

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

            // Create or update stats entity
            if (existingStats == null)
            {
                existingStats = new LaundromatStats
                {
                    LaundromatId = laundromatId,
                    LaundromatName = laundromatName, // Ensure this isn't null
                    PeriodType = periodType,
                    PeriodKey = periodKey,
                    StartDate = startDate,
                    EndDate = endDate,
                    WasherStartPrice = washerStartPrice,
                    DryerStartPrice = dryerStartPrice,
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
                };
                dbContext.LaundromatStats.Add(existingStats);
            }
            else
            {
                // Update date range
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
    }
}
