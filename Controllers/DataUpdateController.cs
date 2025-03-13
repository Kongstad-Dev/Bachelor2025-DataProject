using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bach2025_nortec.Database;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using DotNetEnv;

[ApiController]
[Route("api/[controller]")]
public class DataUpdateController : ControllerBase
{
    private readonly ExternalApiService _externalApiService;
    private readonly YourDbContext _dbContext;
    private readonly string _locationsApiUrl;
    private readonly string _transactionsApiUrl;

    public DataUpdateController(ExternalApiService externalApiService, YourDbContext dbContext)
    {
        _externalApiService = externalApiService;
        _dbContext = dbContext;
        _locationsApiUrl = Env.GetString("API_LOCATIONS");
        _transactionsApiUrl = Env.GetString("API_TRANSACTIIONS");
    }

    // Adds new laundromats and banks to the database
    [HttpPost("update-all-laundromats")]
    public async Task<IActionResult> UpdateAllLaundromatsAndBanks()
    {
        string baseEndpoint = _locationsApiUrl;

        (string baseUrl, int initialPageNumber) = ParseEndpoint(baseEndpoint);

        int totalLaundromats = await FetchAndProcessAllLaundromatPages(baseUrl, initialPageNumber);

        return Ok($"Data update completed. Added {totalLaundromats} new laundromats.");
    }

    [HttpPost("update-all-transactions")]
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

    [HttpPost("update-laundromat-transactions/{kId}")]
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

        string transactionEndpoint = $"{_transactionsApiUrl}{laundromatId}/{lastFetchDate}/{currentDate}";

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

    private (string baseUrl, int initialPage) ParseEndpoint(string endpoint)
    {
        string baseUrl;
        int pageNumber = 0;

        int lastSlashIndex = endpoint.LastIndexOf('/');
        if (lastSlashIndex != -1)
        {
            string lastSegment = endpoint.Substring(lastSlashIndex + 1);
            if (int.TryParse(lastSegment, out pageNumber))
            {
                baseUrl = endpoint.Substring(0, lastSlashIndex);
            }
            else
            {
                baseUrl = endpoint;
            }
        }
        else
        {
            baseUrl = endpoint;
        }

        return (baseUrl, pageNumber);
    }

    private async Task<int> FetchAndProcessAllLaundromatPages(string baseUrl, int startPage)
    {
        int totalLaundromats = 0;
        bool hasMoreData = true;
        int pageNumber = startPage;

        // Continue fetching pages until no more data is returned or limit is reached
        while (hasMoreData && pageNumber < 100)
        {
            string currentEndpoint = $"{baseUrl}/{pageNumber}";
            var data = await _externalApiService.FetchDataAsync(currentEndpoint);

            if (string.IsNullOrEmpty(data))
            {
                hasMoreData = false;
                continue;
            }

            var laundromats = JsonConvert.DeserializeObject<List<Laundromat>>(data);
            if (laundromats == null || laundromats.Count == 0)
            {
                hasMoreData = false;
                continue;
            }

            int newLaundromats = await ProcessLaundromats(laundromats);
            totalLaundromats += newLaundromats;

            pageNumber++;
        }

        return totalLaundromats;
    }

    private async Task<int> ProcessLaundromats(List<Laundromat> laundromats)
    {
        int newLaundromatCount = 0;

        foreach (var laundromat in laundromats)
        {
            var existingLaundromat = _dbContext.Laundromat.SingleOrDefault(l =>
                l.kId == laundromat.kId
            );
            if (existingLaundromat == null)
            {
                var bank = await GetOrCreateBank(laundromat.bank);

                // Create new laundromat with the bank reference
                var newLaundromat = new Laundromat
                {
                    kId = laundromat.kId,
                    externalId = laundromat.externalId,
                    bank = laundromat.bank,
                    name = laundromat.name,
                    zip = laundromat.zip,
                    longitude = laundromat.longitude,
                    latitude = laundromat.latitude,
                    bId = bank.bId,
                };
                _dbContext.Laundromat.Add(newLaundromat);
                newLaundromatCount++;
            }
        }

        await _dbContext.SaveChangesAsync();
        return newLaundromatCount;
    }

    private async Task<BankEntity> GetOrCreateBank(string bankName)
    {
        var bank = _dbContext.Bank.SingleOrDefault(b => b.name == bankName);
        if (bank == null)
        {
            bank = new BankEntity { name = bankName };
            _dbContext.Bank.Add(bank);
            await _dbContext.SaveChangesAsync();
        }
        return bank;
    }
}
