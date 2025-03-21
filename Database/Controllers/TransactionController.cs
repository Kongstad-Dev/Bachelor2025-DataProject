using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bach2025_nortec.Database;
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

    public TransactionController(ExternalApiService externalApiService, YourDbContext dbContext)
    {
        _externalApiService = externalApiService;
        _dbContext = dbContext;
        _transactionsApiUrl = Env.GetString("API_TRANSACTIIONS");
    }

    // Adds new transactions to the database for all laundromats
    // This method is intended to be called during daily update
    [HttpPost("update-all")]
    public async Task<IActionResult> UpdateAllTransactions()
    {
        // Get all laundromats from database
        var laundromats = _dbContext.Laundromat.ToList();

        int totalTransactions = 0;

        foreach (var laundromat in laundromats)
        {
            if (laundromat == null || string.IsNullOrEmpty(laundromat.kId))
            {
                System.Console.WriteLine("Laundromat or kId is null");
                continue;
            }
            // Call method to update transactions for each laundromat
            int transactionsAdded = await UpdateTransactionsForLaundromat(laundromat.kId);
            totalTransactions += transactionsAdded;
        }

        return Ok($"Transaction update completed. Added {totalTransactions} new transactions.");
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
            .Laundromat.Where(l => laundromatIds.Contains(l.kId))
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
        var query = _dbContext.Transactions.Where(t => laundromatIds.Contains(t.LaundromatId));

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
            .GroupBy(t => t.LaundromatId)
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
        int newTransactionCount = 0;

        foreach (var transaction in transactions)
        {
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør
            // Maybe dont add transactions with unitType or unitName caontaining Dør

            // Check if transaction already exists in database
            var existingTransaction = _dbContext.Transactions.SingleOrDefault(t =>
                t.kId == transaction.kId
            );

            if (existingTransaction == null)
            {
                // Set the laundromat ID for the transaction
                transaction.LaundromatId = laundromatId;

                _dbContext.Transactions.Add(transaction);
                newTransactionCount++;
            }
        }

        await _dbContext.SaveChangesAsync();
        return newTransactionCount;
    }
}
