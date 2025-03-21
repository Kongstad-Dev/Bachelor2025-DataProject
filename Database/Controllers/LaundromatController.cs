using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

[ApiController]
[Route("api/[controller]")]
public class LaundromatController : ControllerBase
{
    private readonly ExternalApiService _externalApiService;
    private readonly YourDbContext _dbContext;
    private readonly string _locationsApiUrl;

    public LaundromatController(ExternalApiService externalApiService, YourDbContext dbContext)
    {
        _externalApiService = externalApiService;
        _dbContext = dbContext;
        _locationsApiUrl = Env.GetString("API_LOCATIONS");
    }

    // Adds new laundromats and banks to the database
    // This method is intended to be called during daily update
    [HttpPost("update-all")]
    public async Task<IActionResult> UpdateAllLaundromatsAndBanks()
    {
        string baseEndpoint = _locationsApiUrl;

        (string baseUrl, int initialPageNumber) = ParseEndpoint(baseEndpoint);

        int totalLaundromats = await FetchAndProcessAllLaundromatPages(baseUrl, initialPageNumber);

        return Ok($"Data update completed. Added {totalLaundromats} new laundromats.");
    }

    // Returns all laundromats with optional filtering and pagination
    [HttpGet]
    public async Task<IActionResult> GetLaundromats(
        [FromQuery] bool all = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? bankName = null,
        [FromQuery] string? zip = null,
        [FromQuery] string? searchTerm = null
    )
    {
        // Build query with filters
        var query = _dbContext.Laundromat.AsQueryable();

        if (!string.IsNullOrEmpty(bankName))
        {
            query = query.Where(l => l.bank == bankName);
        }

        if (!string.IsNullOrEmpty(zip))
        {
            query = query.Where(l => l.zip == zip);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(l =>
                (l.name != null && l.name.ToLower().Contains(searchTerm))
                || (l.bank != null && l.bank.ToLower().Contains(searchTerm))
            );
        }

        // Get total count for metadata
        var totalCount = await query.CountAsync();

        // Apply pagination unless "all" is requested
        var laundromats = all
            ? await query.OrderBy(l => l.name).ToListAsync()
            : await query
                .OrderBy(l => l.name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        // Return result with pagination metadata
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
                Laundromats = laundromats,
            }
        );
    }

    // Get a specific laundromat by ID
    [HttpGet("{kId}")]
    public async Task<IActionResult> GetLaundromat(string kId)
    {
        if (string.IsNullOrEmpty(kId))
        {
            return BadRequest("Laundromat kId is required");
        }

        // Build query with includes based on parameters
        IQueryable<Laundromat> query = _dbContext.Laundromat.AsQueryable();

        // Get the laundromat
        var laundromat = await query.FirstOrDefaultAsync(l => l.kId == kId);

        if (laundromat == null)
        {
            return NotFound($"Laundromat with kId {kId} not found");
        }

        return Ok(laundromat);
    }

    //Get all laundromats from a specific bank
    [HttpGet("bank/{bId}")]
    public async Task<IActionResult> GetLaundromatsByBank(int bId)
    {
        // Load the bank and include its laundromats in a single query
        var bank = await _dbContext
            .Bank.Include(b => b.Laundromats)
            .FirstOrDefaultAsync(b => b.bId == bId);

        if (bank == null)
        {
            return NotFound($"Bank with bId {bId} not found");
        }

        var response = new
        {
            Bank = new { bank.bId, bank.name },
            Laundromats = bank
                .Laundromats.OrderBy(l => l.name)
                .Select(l => new
                {
                    l.kId,
                    l.externalId,
                    bankName = l.bank,
                    l.bId,
                    l.name,
                    l.zip,
                    l.longitude,
                    l.latitude,
                    l.lastFetchDate,
                })
                .ToList(),
        };

        return Ok(response);
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
