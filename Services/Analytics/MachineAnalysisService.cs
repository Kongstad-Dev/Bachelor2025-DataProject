using BlazorTest.Database;
using BlazorTest.Services.Analytics.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services.Analytics
{
    public class MachineAnalysisService : BaseAnalysisService
    {
        public MachineAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        ) : base(dbContextFactory, cache)
        {
        }

        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
        )> getStackedMachineStarts(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            // Get first 10 laundromatids
            var firstLaundromatsIds = laundromatIds.Take(10).ToList();

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => firstLaundromatsIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            // Fetch transactions within the date range
            var transactions = await dbContext
                .Transactions.AsNoTracking()
                .Where(t =>
                    firstLaundromatsIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.amount != 0
                )
                .ToListAsync();

            // Get all unique unitNames
            var uniqueUnitNames = transactions
                .Select(t => t.unitName)
                .Distinct()
                .Select(name => {
                    // Extract prefix and number parts with regex
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"^([^\d]+)(\d+)?");
                    string prefix = match.Groups[1].Value.Trim();
                    int number = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                    return new { Name = name, Prefix = prefix, Number = number };
                })
                .OrderBy(item => item.Prefix)
                .ThenBy(item => item.Number)
                .Select(item => item.Name)
                .ToList();

            // Group transactions by laundromat and unitName
            var groupedData = transactions
                .GroupBy(t => new { t.LaundromatId, t.unitName })
                .Select(g => new
                {
                    LaundromatId = g.Key.LaundromatId,
                    UnitName = g.Key.unitName,
                    Count = g.Count(),
                })
                .ToList();

            // Map grouped data to laundromat names and create a decimal[][] for unit counts
            var labels = laundromats.Select(l => l.name ?? $"ID {l.kId}").ToArray();
            var values = laundromats
                .Select(l =>
                    uniqueUnitNames
                        .Select(unitName =>
                            groupedData
                                .Where(g => g.LaundromatId == l.kId && g.UnitName == unitName)
                                .Sum(g => (decimal)g.Count)
                        )
                        .ToArray()
                )
                .ToArray();
            var unitNames = uniqueUnitNames.ToArray();

            return (labels, values, unitNames);
        }

        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
            )> getStackedMachineRevenue(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
             // Get first 10 laundromatids
            var firstLaundromatsIds = laundromatIds.Take(10).ToList();

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Fetch laundromats
            var laundromats = await dbContext
                .Laundromat.AsNoTracking()
                .Where(l => firstLaundromatsIds.Contains(l.kId))
                .Select(l => new { l.kId, l.name })
                .ToListAsync();

            // Fetch transactions within the date range
            var transactions = await dbContext
                .Transactions.AsNoTracking()
                .Where(t =>
                    firstLaundromatsIds.Contains(t.LaundromatId)
                    && t.date >= startDate
                    && t.date <= endDate
                    && t.amount != 0
                )
                .ToListAsync();

            // Get all unique unitNames
            var uniqueUnitNames = transactions
                .Select(t => t.unitName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            // Group transactions by laundromat and unitName
            var groupedData = transactions
                .GroupBy(t => new { t.LaundromatId, t.unitName })
                .Select(g => new
                {
                    LaundromatId = g.Key.LaundromatId,
                    UnitName = g.Key.unitName,
                    amount = g.Sum(t => Math.Abs(t.amount)) / 100m,
                })
                .ToList();

            // Map grouped data to laundromat names and create a decimal[][] for unit counts
            var labels = laundromats.Select(l => l.name ?? $"ID {l.kId}").ToArray();
            var values = laundromats
                .Select(l =>
                    uniqueUnitNames
                        .Select(unitName =>
                            groupedData
                                .Where(g => g.LaundromatId == l.kId && g.UnitName == unitName)
                                .Sum(g => g.amount)
                        )
                        .ToArray()
                )
                .ToArray();
            var unitNames = uniqueUnitNames.ToArray();

            return (labels, values, unitNames);
        }

        public async Task<Dictionary<string, List<MachineDetailRow>>> GetMachineDetailsByLaundromat(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate,
            string metricKey)
        {
            // Create cache key for the request
            string cacheKey = $"machine_details_{string.Join("_", laundromatIds.OrderBy(id => id))}_{startDate?.ToString("yyyyMMdd")}_{endDate?.ToString("yyyyMMdd")}_{metricKey}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, List<MachineDetailRow>> cachedResult))
            {
                return cachedResult;
            }
            
            var result = new Dictionary<string, List<MachineDetailRow>>();
            
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                
                // Fetch laundromats for names
                var laundromats = await dbContext.Laundromat.AsNoTracking()
                    .Where(l => laundromatIds.Contains(l.kId))
                    .ToListAsync();
                
                if (laundromats == null || !laundromats.Any())
                    return result;
                
                // Define dryer unit types for filtering
                var dryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };
                
                // Get transactions for these laundromats in the date range
                var transactions = await dbContext.Transactions
                    .Where(t => laundromatIds.Contains(t.LaundromatId))
                    .Where(t => startDate == null || t.date >= startDate)
                    .Where(t => endDate == null || t.date <= endDate)
                    .Where(t => t.amount != 0)  // Skip zero-amount transactions
                    .AsNoTracking()
                    .ToListAsync();
                
                // Process each laundromat (including ones with no transactions)
                foreach (var laundromat in laundromats)
                {
                    var laundromatName = laundromat.name ?? laundromat.kId;
                    var laundromatTransactions = transactions.Where(t => t.LaundromatId == laundromat.kId).ToList();
                    
                    var machineRows = new List<MachineDetailRow>();
                    
                    if (laundromatTransactions.Any())
                    {
                        var groupedTransactions = laundromatTransactions
                            .GroupBy(t => t.unitName ?? "Unknown")
                            .Select(g => new {
                                MachineName = g.Key,
                                Starts = g.Count(),
                                Revenue = g.Sum(t => Math.Abs(t.amount)) / 100m,
                                IsWasher = !g.Any(t => dryerUnitTypes.Contains(t.unitType))
                            })
                            .ToList();
                            
                        foreach (var group in groupedTransactions)
                        {
                            // Calculate price per start correctly
                            decimal pricePerStart = group.Starts > 0 ? group.Revenue / group.Starts : 0;
                            
                            machineRows.Add(new MachineDetailRow
                            {
                                MachineName = group.MachineName,
                                Starts = group.Starts,
                                Revenue = group.Revenue,
                                IsWasher = group.IsWasher,
                                PricePerStart = pricePerStart
                            });
                        }
                        
                        // Sort the machines based on the metric key
                        if (metricKey.Contains("Revenue", StringComparison.OrdinalIgnoreCase))
                            machineRows = machineRows.OrderByDescending(r => r.Revenue).ToList();
                        else if (metricKey.Contains("Starts", StringComparison.OrdinalIgnoreCase) || metricKey.Contains("Count", StringComparison.OrdinalIgnoreCase))
                            machineRows = machineRows.OrderByDescending(r => r.Starts).ToList();
                        else
                            machineRows = machineRows.OrderByDescending(r => r.Revenue).ToList();
                    }
                    
                    // Always add the laundromat, even if it has no machines/transactions
                    result.Add(laundromatName, machineRows);
                }
                
                // Sort laundromats by total revenue (highest first)
                result = result
                    .OrderByDescending(kvp => kvp.Value.Sum(m => m.Revenue))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    
                // Cache the result for 5 minutes
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving machine data: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            return result;
        }
    }
}