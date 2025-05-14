using BlazorTest.Database.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorTest.Services
{
    public class TransactionService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly ExternalApiService _externalApiService;
        private readonly ILogger<TransactionService> _logger;
        private readonly string _transactionsApiUrl;

        public TransactionService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            ExternalApiService externalApiService,
            ILogger<TransactionService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _externalApiService = externalApiService;
            _logger = logger;
            _transactionsApiUrl = Environment.GetEnvironmentVariable("API_TRANSACTIIONS");
        }

        public async Task<List<TransactionEntity>> GetTransactionsAsync(
            List<string> laundromatIds = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 100)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.Transactions.AsNoTracking();

            if (laundromatIds != null && laundromatIds.Any())
            {
                query = query.Where(t => laundromatIds.Contains(t.LaundromatId));
            }

            if (startDate.HasValue)
            {
                query = query.Where(t => t.date >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.date <= endDate.Value);
            }

            // Apply pagination
            var skip = (pageNumber - 1) * pageSize;
            return await query
                .OrderByDescending(t => t.date)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<TransactionEntity> GetTransactionByIdAsync(string id)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.kId == id);
        }

        public async Task<int> GetTransactionCountAsync(
            List<string> laundromatIds = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.Transactions.AsNoTracking();

            if (laundromatIds != null && laundromatIds.Any())
            {
                query = query.Where(t => laundromatIds.Contains(t.LaundromatId));
            }

            if (startDate.HasValue)
            {
                query = query.Where(t => t.date >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.date <= endDate.Value);
            }

            return await query.CountAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync(
            List<string> laundromatIds = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.Transactions.AsNoTracking();

            if (laundromatIds != null && laundromatIds.Any())
            {
                query = query.Where(t => laundromatIds.Contains(t.LaundromatId));
            }

            if (startDate.HasValue)
            {
                query = query.Where(t => t.date >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.date <= endDate.Value);
            }

            // Calculate total revenue in decimal (assuming amount is in cents)
            var totalAmountInCents = await query.SumAsync(t => t.amount);
            return totalAmountInCents / 100m;
        }
        
        public async Task<(int totalTransactions, int failedLaundromats)> UpdateAllTransactionsAsync()
        {
            _logger.LogInformation("Starting transaction update for all laundromats");
            
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var laundromats = await dbContext.Laundromat.ToListAsync();

            int totalTransactions = 0;
            int failedLaundromats = 0;

            foreach (var laundromat in laundromats)
            {
                if (laundromat == null || string.IsNullOrEmpty(laundromat.kId))
                {
                    _logger.LogWarning("Laundromat or kId is null");
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
                        transactionsAdded = await UpdateTransactionsForLaundromatAsync(laundromat.kId);
                        success = true;
                        totalTransactions += transactionsAdded;

                        // Log success
                        _logger.LogInformation($"Successfully updated transactions for laundromat {laundromat.kId}, added {transactionsAdded} transactions");
                    }
                    catch (Exception ex)
                    {
                        retryCount++;

                        // Log the error
                        _logger.LogError(ex, $"Error updating transactions for laundromat {laundromat.kId} (Attempt {retryCount}/3)");

                        if (retryCount < 3)
                        {
                            // Exponential backoff
                            int delayMilliseconds = (int)Math.Pow(2, retryCount) * 1000;
                            _logger.LogInformation($"Retrying in {delayMilliseconds / 1000} seconds...");
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

            _logger.LogInformation(message);
            return (totalTransactions, failedLaundromats);
        }

        public async Task<int> UpdateTransactionsForLaundromatAsync(string laundromatId)
        {
            if (string.IsNullOrEmpty(laundromatId))
            {
                throw new ArgumentException("Laundromat ID cannot be null or empty");
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var laundromat = await dbContext.Laundromat
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.kId == laundromatId);

            if (laundromat == null)
            {
                _logger.LogWarning($"Laundromat not found in database: {laundromatId}");
                return 0;
            }

            var lastFetchDate = laundromat.lastFetchDate?.ToString("yyyy-MM-dd") ?? "2025-01-01";
            var currentDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

            string transactionEndpoint = $"{_transactionsApiUrl}{laundromatId}/{lastFetchDate}/{currentDate}";

            var data = await _externalApiService.FetchDataAsync(transactionEndpoint);

            if (string.IsNullOrEmpty(data))
            {
                _logger.LogInformation($"No data found for laundromat with kId {laundromatId}");
                return 0;
            }

            var transactions = JsonConvert.DeserializeObject<List<TransactionEntity>>(data);
            if (transactions == null || transactions.Count == 0)
            {
                _logger.LogInformation($"No transactions found for laundromat with kId {laundromatId}");
                return 0;
            }

            // Process and save transactions
            int newTransactions = await ProcessTransactions(transactions, laundromatId);

            // Update last fetch date for laundromat in a separate context
            using var updateContext = await _dbContextFactory.CreateDbContextAsync();
            var laundromatToUpdate = await updateContext.Laundromat
                .FirstOrDefaultAsync(l => l.kId == laundromatId);

            if (laundromatToUpdate != null)
            {
                try
                {
                    // Get LaundromatStatsService to update stats
                    using var scope = _logger.BeginScope("UpdateStats");
                    
                    // Update the laundromat's last fetch date
                    laundromatToUpdate.lastFetchDate = DateTime.Now;
                    updateContext.Entry(laundromatToUpdate).State = EntityState.Modified;
                    await updateContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating lastFetchDate for laundromat {laundromatId}");
                }
            }
            else
            {
                _logger.LogWarning($"Could not find laundromat with kId {laundromatId} for updating lastFetchDate");
            }

            return newTransactions;
        }

        public async Task<int> ResetAllFetchDatesAsync()
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var laundromatIds = await dbContext.Laundromat
                    .AsNoTracking()
                    .Select(l => l.kId)
                    .ToListAsync();

                int count = 0;

                foreach (var id in laundromatIds)
                {
                    // Find each entity separately to ensure we have the latest version
                    var laundromat = await dbContext.Laundromat.FindAsync(id);

                    if (laundromat != null)
                    {
                        // Reset the lastFetchDate
                        laundromat.lastFetchDate = null;
                        dbContext.Entry(laundromat).State = EntityState.Modified;
                        count++;
                    }
                }

                // Save all changes at once
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"Reset lastFetchDate for {count} laundromats");
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting fetch dates");
                throw;
            }
        }

        private async Task<int> ProcessTransactions(List<TransactionEntity> transactions, string laundromatId)
        {
            var filteredTransactions = transactions
                .Where(t => 
                    t.seconds != 0 || 
                    (t.seconds == 0 && 
                    t.debug != null && 
                    t.debug.Contains(" ") && 
                    t.debug.Substring(t.debug.IndexOf(' ') + 1).StartsWith("Start", StringComparison.OrdinalIgnoreCase))
                )
                .ToList();

            if (!filteredTransactions.Any())
            {
                return 0;
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

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
                using var batchContext = await _dbContextFactory.CreateDbContextAsync();
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
                    _logger.LogError(ex, "Error processing transaction batch");

                    // Try individually if batch fails
                    foreach (var transaction in batch)
                    {
                        try
                        {
                            using var singleContext = await _dbContextFactory.CreateDbContextAsync();
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
                            _logger.LogError(innerEx, $"Error adding transaction {transaction.kId}");
                        }
                    }
                }
            }

            return newTransactionCount;
        }
    }
}