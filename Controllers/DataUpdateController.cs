using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bach2025_nortec.Database;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class DataUpdateController : ControllerBase
{
    private readonly ExternalApiService _externalApiService;
    private readonly YourDbContext _dbContext;

    public DataUpdateController(ExternalApiService externalApiService, YourDbContext dbContext)
    {
        _externalApiService = externalApiService;
        _dbContext = dbContext;
    }

    // Renamed endpoint for updating all laundromats
    [HttpPost("update-all-laundromats")]
    public async Task<IActionResult> UpdateAllLaundromatsAndBanks()
    {
        string baseEndpoint =
            "https://datpro2.api.kombine.services/AleksanderKristoffer/Locations1/3E7Q77379418h77359418M/500/0";

        (string baseUrl, int initialPageNumber) = ParseEndpoint(baseEndpoint);

        int totalLaundromats = await FetchAndProcessAllLaundromatPages(baseUrl, initialPageNumber);

        return Ok($"Data update completed. Added {totalLaundromats} new laundromats.");
    }

    // New endpoint for updating a single laundromat by ID
    [HttpPost("update-laundromat/{kId}")]
    public async Task<IActionResult> UpdateSingleLaundromat(string kId)
    {
        if (string.IsNullOrEmpty(kId))
        {
            return BadRequest("Laundromat kId is required");
        }

        // Check if laundromat already exists
        var existingLaundromat = _dbContext.Laundromat.SingleOrDefault(l => l.kId == kId);
        if (existingLaundromat != null)
        {
            return Ok($"Laundromat with kId {kId} already exists in database");
        }

        // Fetch the specific laundromat from the API
        string specificEndpoint =
            $"https://datpro2.api.kombine.services/AleksanderKristoffer/Location/{kId}";

        var data = await _externalApiService.FetchDataAsync(specificEndpoint);

        if (string.IsNullOrEmpty(data))
        {
            return NotFound($"No data found for laundromat with kId {kId}");
        }

        var laundromat = JsonConvert.DeserializeObject<Laundromat>(data);
        if (laundromat == null)
        {
            return NotFound($"Could not parse data for laundromat with kId {kId}");
        }

        // Process the single laundromat
        var bank = await GetOrCreateBank(laundromat.bank);

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
        await _dbContext.SaveChangesAsync();

        return Ok($"Laundromat with kId {kId} added successfully");
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
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity
        //Should get lastFetchDate from laundromat entity

        // Replace with your actual transaction API endpoint
        string transactionEndpoint =
            $"https://datpro2.api.kombine.services/AleksanderKristoffer/Transactions1/{laundromatId}/{lastFetchDate}/{currentDate}";

        (string baseUrl, int initialPageNumber) = ParseEndpoint(transactionEndpoint);
        // Continue fetching pages until no more data is returned
        ;
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
        
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity
        //Should update lastFetchDate in laundromat entity

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
