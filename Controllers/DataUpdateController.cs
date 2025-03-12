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

    [HttpPost("update-laundromats-banks")]
    public async Task<IActionResult> UpdateDataFromApi1()
    {
        string baseEndpoint = "https://datpro2.api.kombine.services/AleksanderKristoffer/Locations1/3E7Q77379418h77359418M/500/0";
        
        (string baseUrl, int initialPageNumber) = ParseEndpoint(baseEndpoint);
        
        int totalLaundromats = await FetchAndProcessAllLaundromatPages(baseUrl, initialPageNumber);

        return Ok($"Data update completed. Added {totalLaundromats} new laundromats.");
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

        // Continue fetching pages until no more data is returned
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
            var existingLaundromat = _dbContext.Laundromat.SingleOrDefault(l => l.kId == laundromat.kId);
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