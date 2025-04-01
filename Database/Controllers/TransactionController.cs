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

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly ExternalApiService _externalApiService;
    private readonly YourDbContext _dbContext;
    private readonly string _transactionsApiUrl;
    private readonly DataAnalysisService _dataAnalysisService;


    public TransactionController(ExternalApiService externalApiService, YourDbContext dbContext, DataAnalysisService dataAnalysisService)
    {
        _externalApiService = externalApiService;
        _dbContext = dbContext;
        _transactionsApiUrl = Env.GetString("API_TRANSACTIIONS");
        _dataAnalysisService = dataAnalysisService;

    }

    // Adds new transactions to the database for all laundromats
    // This method is intended to be called during daily update
    [HttpPost("update-all")]
    public async Task<IActionResult> UpdateAllTransactions()
    {
        // Get all laundromats from database
        var laundromats = _dbContext.Laundromat.ToList();

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
                        // Exponential backoff: 2, 4, 8 seconds between retries
                        int delayMilliseconds = (int)Math.Pow(2, retryCount) * 1000;
                        System.Console.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                        await Task.Delay(delayMilliseconds);
                    }
                    else
                    {
                        // Max retries reached, log failure
                        System.Console.WriteLine($"Failed to update transactions for laundromat {laundromat.kId} after 3 attempts");
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
        if (string.IsNullOrEmpty(kId))
        {
            return BadRequest("Laundromat kId is required");
        }

        // Check if laundromat exists
        var laundromat = _dbContext.Laundromat.FirstOrDefault(l => l.kId == kId);
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
        // Validate input
        if (string.IsNullOrEmpty(kId))
            return BadRequest("Laundromat kId is required");

        // Check if laundromat exists
        var laundromatExists = await _dbContext.Laundromat.AnyAsync(l => l.kId == kId);
        if (!laundromatExists)
            return NotFound($"Laundromat with kId {kId} not found");

        // Build query with filters
        var query = _dbContext.Transactions.Where(t => t.LaundromatId == kId);

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
        var laundromat = _dbContext.Laundromat.SingleOrDefault(l => l.kId == laundromatId);
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
            System.Console.WriteLine("No data found for laundromat with kId {laundromatId}");
            return 0;
        }

        var transactions = JsonConvert.DeserializeObject<List<TransactionEntity>>(data);
        if (transactions == null || transactions.Count == 0)
        {
            System.Console.WriteLine(
                "No transactions found for laundromat with kId {laundromatId}"
            );
            return 0;
        }

        // Process and save transactions
        int newTransactions = await ProcessTransactions(transactions, laundromatId);

        // Update last fetch date for laundromat
        laundromat.lastFetchDate = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        return newTransactions;
    }

    private async Task<int> ProcessTransactions(
        List<TransactionEntity> transactions,
        string laundromatId
    )
    {
        // Skip transactions with unitName containing "Dør" (Door)
        var filteredTransactions = transactions
            .Where(t => string.IsNullOrEmpty(t.unitName) || !t.unitName.Contains("Dør", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filteredTransactions.Count == 0)
        {
            return 0;
        }

        // Get all transaction IDs from the incoming batch
        var incomingTransactionIds = filteredTransactions.Select(t => t.kId).ToHashSet();

        // Query the database once to get all existing transaction IDs for this laundromat
        var existingTransactionIds = await _dbContext.Transactions
            .Where(t => incomingTransactionIds.Contains(t.kId))
            .Select(t => t.kId)
            .ToListAsync();

        // Convert to HashSet for O(1) lookup performance
        var existingIdSet = new HashSet<string>(existingTransactionIds);

        int newTransactionCount = 0;

        // Only process transactions that don't already exist
        foreach (var transaction in filteredTransactions)
        {
            if (!existingIdSet.Contains(transaction.kId))
            {
                // Set the laundromat ID for the transaction
                transaction.LaundromatId = laundromatId;

                _dbContext.Transactions.Add(transaction);
                newTransactionCount++;
            }
        }

        if (newTransactionCount > 0)
        {
            await _dbContext.SaveChangesAsync();
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
