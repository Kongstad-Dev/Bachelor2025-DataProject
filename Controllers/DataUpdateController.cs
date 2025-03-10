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

    [HttpPost("update-data-from-api1")]
    public async Task<IActionResult> UpdateDataFromApi1()
    {
        string baseEndpoint =
            "https://datpro2.api.kombine.services/AleksanderKristoffer/Locations1/3E7Q77379418h77359418M/500/0";

        // Check if the baseEndpoint ends with a number
        string baseUrl;
        int pageNumber = 0;

        // Get the base URL up to the last segment
        int lastSlashIndex = baseEndpoint.LastIndexOf('/');
        if (lastSlashIndex != -1)
        {
            string lastSegment = baseEndpoint.Substring(lastSlashIndex + 1);
            if (int.TryParse(lastSegment, out pageNumber))
            {
                baseUrl = baseEndpoint.Substring(0, lastSlashIndex);
            }
            else
            {
                // If the last segment isn't a number, use the entire endpoint as base
                // and start from page 0
                baseUrl = baseEndpoint;
            }
        }
        else
        {
            baseUrl = baseEndpoint;
        }

        int totalLaundromats = 0;
        bool hasMoreData = true;

        // Continue fetching pages until no more data is returned
        while (hasMoreData && pageNumber < 100)
        {
            string currentEndpoint = $"{baseUrl}/{pageNumber}";
            var data = await _externalApiService.FetchDataAsync(currentEndpoint);

            if (string.IsNullOrEmpty(data))
            {
                // No more data
                hasMoreData = false;
                continue;
            }

            var laundromats = JsonConvert.DeserializeObject<List<Laundromat>>(data);
            if (laundromats == null || laundromats.Count == 0)
            {
                // No more data
                hasMoreData = false;
                continue;
            }

            foreach (var laundromat in laundromats)
            {
                var existingLaundromat = _dbContext.Laundromat.SingleOrDefault(l =>
                    l.kId == laundromat.kId
                );
                if (existingLaundromat == null)
                {
                    // Check if a bank with the same name exists and create it if it doesn't
                    var bank = _dbContext.Bank.SingleOrDefault(b => b.name == laundromat.bank);
                    if (bank == null)
                    {
                        bank = new BankEntity { name = laundromat.bank };
                        _dbContext.Bank.Add(bank);
                        await _dbContext.SaveChangesAsync(); // Save changes to get the new bank's bId
                    }

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
                    totalLaundromats++;
                }
            }

            await _dbContext.SaveChangesAsync();
            pageNumber++; // Increment page number for the next request
        }

        return Ok($"Data update completed. Added {totalLaundromats} new laundromats.");
    }
}
