using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using BlazorTest.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BlazorTest.Database.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionController : ControllerBase
    {
        private readonly ExternalApiService _externalApiService;
        private readonly YourDbContext _dbContext;
        private readonly string _transactionsApiUrl;
        private readonly DataAnalysisService _dataAnalysisService;
        private readonly LaundromatStatsController _statsController;
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;

        public TransactionController(
            ExternalApiService externalApiService,
            IDbContextFactory<YourDbContext> dbContextFactory,
            DataAnalysisService dataAnalysisService,
            LaundromatStatsController statsController)
        {
            _externalApiService = externalApiService;
            _dbContextFactory = dbContextFactory;
            _transactionsApiUrl = Env.GetString("API_TRANSACTIIONS");
            _dataAnalysisService = dataAnalysisService;
            _statsController = statsController;
        }

        [HttpPost("reset-fetch-dates")]
        public async Task<IActionResult> ResetAllFetchDates()
        {
            try
            {
                // Use AsNoTracking() to avoid any potential tracking conflicts
                var laundromatIds = await _dbContext.Laundromat
                    .AsNoTracking()
                    .Select(l => l.kId)
                    .ToListAsync();

                int count = 0;

                foreach (var id in laundromatIds)
                {
                    // Find each entity separately to ensure we have the latest version
                    var laundromat = await _dbContext.Laundromat.FindAsync(id);

                    if (laundromat != null)
                    {
                        // Reset the lastFetchDate
                        laundromat.lastFetchDate = null;

                        // Explicitly mark the entity as modified
                        _dbContext.Entry(laundromat).State = EntityState.Modified;
                        count++;

                    }
                }

                // Save all changes at once
                await _dbContext.SaveChangesAsync();

                return Ok($"Reset lastFetchDate for {count} laundromats");
            }
            catch (Exception ex)
            {
                // Log the detailed exception
                System.Console.WriteLine($"Error resetting fetch dates: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");

                return StatusCode(500, $"Error resetting lastFetchDate: {ex.Message}");
            }
        }

        // Adds new transactions to the database for all laundromats
        // This method is intended to be called during daily update
        [HttpPost("update-all")]
        public async Task<IActionResult> UpdateAllTransactions()
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Get all laundromats from database
            var laundromats = await dbContext.Laundromat.ToListAsync();

            int totalTransactions = 0;
            int failedLaundromats = 0;

            foreach (var laundromat in laundromats)
            {
                if (laundromat == null || string.IsNullOrEmpty(laundromat.kId))
                {
                    System.Console.WriteLine("Laundromat or kId is null");
                    continue;
                }

                // Add retry logic with exponential backoff
                int retryCount = 0;
                bool success = false;
                int transactionsAdded = 0;

                while (!success && retryCount < 3) // Maximum 3 retry attempts
                {
                    try
                    {
                        // Call method to update transactions for each laundromat
                        transactionsAdded = await UpdateTransactionsForLaundromat(laundromat.kId);
                        success = true;
                        totalTransactions += transactionsAdded;

                        // Log success
                        System.Console.WriteLine($"Successfully updated transactions for laundromat {laundromat.kId}, added {transactionsAdded} transactions");
                    }
                    catch (Exception ex)
                    {
                        retryCount++;

                        // Log the error
                        System.Console.WriteLine($"Error updating transactions for laundromat {laundromat.kId} (Attempt {retryCount}/3): {ex.Message}");

                        if (retryCount < 3)
                        {
                            // Exponential backoff
                            int delayMilliseconds = (int)Math.Pow(2, retryCount) * 1000;
                            System.Console.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                            await Task.Delay(delayMilliseconds);
                        }
                        else
                        {
                            failedLaundromats++;
                        }
                    }
                }
            }

            string message = $"Transaction update completed. Added {totalTransactions} new transactions.";
            if (failedLaundromats > 0)
            {
                message += $" Failed to update {failedLaundromats} laundromats after multiple attempts.";
            }

            return Ok(message);
        }

        // Adds new transactions to the database for a specific laundromat
        [HttpPost("update-laundromat/{kId}")]
        public async Task<IActionResult> UpdateLaundromatTransactions(string kId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            if (string.IsNullOrEmpty(kId))
            {
                return BadRequest("Laundromat kId is required");
            }

            // Check if laundromat exists
            var laundromat = await dbContext.Laundromat.FirstOrDefaultAsync(l => l.kId == kId);
            if (laundromat == null)
            {
                return NotFound($"Laundromat with kId {kId} not found");
            }

            int transactionsAdded = await UpdateTransactionsForLaundromat(kId);

            return Ok(
                $"Transaction update completed for laundromat {kId}. Added {transactionsAdded} new transactions."
            );
        }

        // Get all transactions with optional filters
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] bool all = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null
        )
        {
            // Build query with filters
            var query = _dbContext.Transactions.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(t => t.date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.date <= endDate.Value);

            // Get total count for pagination metadata
            var totalCount = await query.CountAsync();

            var orderedQuery = query.OrderByDescending(t => t.date);

            // Skip pagination if "all" is set
            var transactions = all
                ? await orderedQuery.ToListAsync() // No pagination
                : await orderedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Return result
            return Ok(
                new
                {
                    PageInfo = new
                    {
                        CurrentPage = all ? 1 : page,
                        PageSize = all ? totalCount : pageSize,
                        TotalCount = totalCount,
                        TotalPages = all ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize),
                        AllRecordsReturned = all,
                    },
                    Transactions = transactions,
                }
            );
        }

        // Get transactions for a specific laundromat with optional filters
        [HttpGet("laundromat/{kId}")]
        public async Task<IActionResult> GetTransactionsForLaundromat(
            string kId,
            [FromQuery] bool all = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            // Validate input
            if (string.IsNullOrEmpty(kId))
                return BadRequest("Laundromat kId is required");

            // Check if laundromat exists
            var laundromatExists = await dbContext.Laundromat.AnyAsync(l => l.kId == kId);
            if (!laundromatExists)
                return NotFound($"Laundromat with kId {kId} not found");

            // Build query with filters
            var query = dbContext.Transactions.Where(t => t.LaundromatId == kId);

            if (startDate.HasValue)
                query = query.Where(t => t.date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.date <= endDate.Value);

            // Get total count for metadata
            var totalCount = await query.CountAsync();

            // Apply ordering
            var orderedQuery = query.OrderByDescending(t => t.date);

            // Skip pagination if "all" is set
            var transactions = all
                ? await orderedQuery.ToListAsync()
                : await orderedQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            // Return result
            return Ok(
                new
                {
                    PageInfo = new
                    {
                        CurrentPage = all ? 1 : page,
                        PageSize = all ? totalCount : pageSize,
                        TotalCount = totalCount,
                        TotalPages = all ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize),
                        AllRecordsReturned = all,
                    },
                    Transactions = transactions,
                }
            );
        }

        // Get transactions for multiple laundromats with optional filters
        [HttpPost("laundromat-list")]
        public async Task<IActionResult> GetTransactionsForMultipleLaundromats(
            [FromBody] List<string> laundromatIds,
            [FromQuery] bool all = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null
        )
        {
            // Validate input
            if (laundromatIds == null || laundromatIds.Count == 0)
                return BadRequest("At least one laundromat ID is required");

            // Check for invalid IDs
            var validLaundromatIds = await _dbContext
                .Laundromat.Where(l => l.kId != null && laundromatIds.Contains(l.kId))
                .Select(l => l.kId)
                .ToListAsync();

            var invalidIds = laundromatIds.Except(validLaundromatIds).ToList();
            if (invalidIds.Any())
            {
                return BadRequest(
                    $"The following laundromat IDs were not found: {string.Join(", ", invalidIds)}"
                );
            }

            // Build query with filters
            var query = _dbContext.Transactions.Where(
                t => t.LaundromatId != null && laundromatIds.Contains(t.LaundromatId)
            );

            if (startDate.HasValue)
                query = query.Where(t => t.date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.date <= endDate.Value);

            // Get total count for metadata
            var totalCount = await query.CountAsync();

            // Apply ordering
            var orderedQuery = query.OrderByDescending(t => t.date);

            // Skip pagination if "all" is set
            var transactions = all
                ? await orderedQuery.ToListAsync() // No pagination
                : await orderedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Group by laundromat ID for better client-side usage
            var groupedTransactions = transactions
                .Where(t => t.LaundromatId != null)
                .GroupBy(t => t.LaundromatId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Return result
            return Ok(
                new
                {
                    PageInfo = new
                    {
                        CurrentPage = all ? 1 : page,
                        PageSize = all ? totalCount : pageSize,
                        TotalCount = totalCount,
                        TotalPages = all ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize),
                        AllRecordsReturned = all,
                    },
                    LaundromatTransactions = groupedTransactions,
                }
            );
        }

        private async Task<int> UpdateTransactionsForLaundromat(string laundromatId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var laundromat = await dbContext.Laundromat
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.kId == laundromatId);

            if (laundromat == null)
            {
                System.Console.WriteLine("Laundromat not found in database");
                return 0;
            }

            var lastFetchDate = laundromat.lastFetchDate?.ToString("yyyy-MM-dd") ?? "2025-01-01";
            var currentDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            string transactionEndpoint =
                $"{_transactionsApiUrl}{laundromatId}/{lastFetchDate}/{currentDate}";

            var data = await _externalApiService.FetchDataAsync(transactionEndpoint);

            if (string.IsNullOrEmpty(data))
            {
                System.Console.WriteLine($"No data found for laundromat with kId {laundromatId}");
                return 0;
            }

            var transactions = JsonConvert.DeserializeObject<List<TransactionEntity>>(data);
            if (transactions == null || transactions.Count == 0)
            {
                System.Console.WriteLine($"No transactions found for laundromat with kId {laundromatId}");
                return 0;
            }

            // Process and save transactions
            int newTransactions = await ProcessTransactions(transactions, laundromatId);

            if (newTransactions > 0)
            {
                // Update last fetch date for laundromat in a separate context
                using var updateContext = _dbContextFactory.CreateDbContext();

                // THIS IS THE ISSUE: FindAsync doesn't work as expected here because
                // while kId is the primary key, it's not being matched correctly
                // var laundromatToUpdate = await updateContext.Laundromat.FindAsync(laundromatId);

                // INSTEAD, use this:
                var laundromatToUpdate = await updateContext.Laundromat
                    .FirstOrDefaultAsync(l => l.kId == laundromatId);

                if (laundromatToUpdate != null)
                {
                    // Add logging to debug
                    Console.WriteLine($"Found laundromat {laundromatId}. Current lastFetchDate: {laundromatToUpdate.lastFetchDate}");

                    laundromatToUpdate.lastFetchDate = DateTime.Now;

                    // Explicitly mark as modified to ensure the update is processed
                    updateContext.Entry(laundromatToUpdate).State = EntityState.Modified;

                    var rowsAffected = await updateContext.SaveChangesAsync();
                    Console.WriteLine($"Updated lastFetchDate to {DateTime.Now}, rows affected: {rowsAffected}");

                    // Call the stats controller's update method directly
                    try
                    {
                        await _statsController.UpdateStats(laundromatId);
                        System.Console.WriteLine($"Stats updated for laundromat {laundromatId}");
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail if stats update has issues
                        System.Console.WriteLine($"Error updating stats: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"WARNING: Could not find laundromat with kId {laundromatId}");
                }
            }

            return newTransactions;
        }

        private async Task<int> ProcessTransactions(
            List<TransactionEntity> transactions,
            string laundromatId
        )
        {
            // Filter out door transactions
            var filteredTransactions = transactions
                .Where(t => string.IsNullOrEmpty(t.unitName) || !t.unitName.Contains("Dør", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!filteredTransactions.Any())
            {
                return 0;
            }

            using var dbContext = _dbContextFactory.CreateDbContext();

            // Get transaction IDs from the incoming batch
            var incomingTransactionIds = filteredTransactions.Select(t => t.kId).ToHashSet();

            // Query existing transaction IDs
            var existingTransactionIds = await dbContext.Transactions
                .AsNoTracking()
                .Where(t => incomingTransactionIds.Contains(t.kId))
                .Select(t => t.kId)
                .ToListAsync();

            var existingIdSet = new HashSet<string>(existingTransactionIds);

            // Process in smaller batches to avoid memory issues
            int newTransactionCount = 0;
            const int batchSize = 100;

            for (int i = 0; i < filteredTransactions.Count; i += batchSize)
            {
                using var batchContext = _dbContextFactory.CreateDbContext();
                var batch = filteredTransactions
                    .Skip(i)
                    .Take(batchSize)
                    .Where(t => !existingIdSet.Contains(t.kId))
                    .ToList();

                if (!batch.Any())
                    continue;

                foreach (var transaction in batch)
                {
                    transaction.LaundromatId = laundromatId;
                    batchContext.Transactions.Add(transaction);
                }

                try
                {
                    var added = await batchContext.SaveChangesAsync();
                    newTransactionCount += added;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error processing batch: {ex.Message}");

                    // Try individually if batch fails
                    foreach (var transaction in batch)
                    {
                        try
                        {
                            using var singleContext = _dbContextFactory.CreateDbContext();
                            if (!existingIdSet.Contains(transaction.kId))
                            {
                                transaction.LaundromatId = laundromatId;
                                singleContext.Transactions.Add(transaction);
                                await singleContext.SaveChangesAsync();
                                newTransactionCount++;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            System.Console.WriteLine($"Error adding transaction {transaction.kId}: {innerEx.Message}");
                        }
                    }
                }
            }

            return newTransactionCount;
        }


        [HttpGet("bank/{bId}/revenue")]
        public async Task<IActionResult> GetBankRevenue(int bId)
        {
            // Ensure the bank exists
            var bankExists = await _dbContext.Bank.AnyAsync(b => b.bId == bId);
            if (!bankExists)
            {
                return NotFound($"Bank with ID {bId} not found");
            }

            // Get laundromat IDs for this bank
            var laundromatIds = await _dbContext.Laundromat
                .Where(l => l.bId == bId)
                .Select(l => l.kId)
                .ToListAsync();

            if (laundromatIds.Count == 0)
            {
                Console.WriteLine($"[API] No laundromats found for bank {bId}");
                return Ok(new { BankId = bId, Revenue = 0 });
            }

            // Find transactions linked to these laundromats
            var transactions = await _dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();

            if (transactions.Count == 0)
            {
                Console.WriteLine($"[API] No transactions found for bank {bId}");
                return Ok(new { BankId = bId, Revenue = 0 });
            }

            // Calculate total revenue
            var totalRevenue = _dataAnalysisService.CalculateRevenueFromTransactions(transactions);

            return Ok(new { BankId = bId, Revenue = totalRevenue });
        }












        [HttpGet("bank/{bId}/soap")]

        public async Task<IActionResult> GetBankSoap(int bId)
        {
            var bankExists = await _dbContext.Bank.AnyAsync(b => b.bId == bId);
            if (!bankExists)
            {
                return NotFound($"Bank with ID {bId} not found");
            }

            // Get laundromat IDs for this bank
            var laundromatIds = await _dbContext.Laundromat
                .Where(l => l.bId == bId)
                .Select(l => l.kId)
                .ToListAsync();

            if (laundromatIds.Count == 0)
            {
                Console.WriteLine($"[API] No laundromats found for bank {bId}");
                return Ok(new { BankId = bId, soap = 0 });
            }

            // Find transactions linked to these laundromats
            var transactions = await _dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId))
                .ToListAsync();

            if (transactions.Count == 0)
            {
                Console.WriteLine($"[API] No transactions found for bank {bId}");
                return Ok(new { BankId = bId, soap = 0 });
            }

            // Calculate total revenue
            var totalAmountSoap = _dataAnalysisService.CalculateTotalSoapProgramFromTransactions(transactions);

            return Ok(new { BankId = bId, soap = totalAmountSoap });
        }






        [HttpGet("bank/{bId}/seconds")]

        public async Task<IActionResult> GetBank_seconds(int bId)
        {
            var bankExists = await _dbContext.Bank.AnyAsync(b => b.bId == bId);
            if (!bankExists)
            {
                return NotFound($"Bank with ID {bId} not found");
            }

            // Get laundromat IDs for this bank
            var laundromatIds = await _dbContext.Laundromat
                .Where(l => l.bId == bId)
                .Select(l => l.kId)
                .ToListAsync();

            if (laundromatIds.Count == 0)
            {
                Console.WriteLine($"[API] No laundromats found for bank {bId}");
                return Ok(new { BankId = bId, seconds = 0 });
            }

            // Find transactions linked to these laundromats
            var transactions = await _dbContext.Transactions
                .Where(t => laundromatIds.Contains(t.LaundromatId) && t.seconds > 0)
                .ToListAsync();

            if (transactions.Count == 0)
            {
                Console.WriteLine($"[API] No transactions found for bank {bId}");
                return Ok(new { BankId = bId, seconds = 0 });
            }

            // Calculate total revenue
            var averageseconds = _dataAnalysisService.CalculateAvgSecoundsFromTransactions(transactions);

            return Ok(new { BankId = bId, seconds = averageseconds });
        }

    }
}