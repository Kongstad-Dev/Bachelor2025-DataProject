using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

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
    public async Task<IActionResult> UpdateDataFromApi1(string endpoint)
    {
        var data = await _externalApiService.FetchDataAsync(endpoint);
        if (string.IsNullOrEmpty(data))
        {
            return BadRequest("No data received from API");
        }
        var laundromats = JsonConvert.DeserializeObject<List<Laundromat>>(data);
        if (laundromats == null || laundromats.Count == 0)
        {
            return BadRequest("No laundromats found in data from API");
        }

        foreach (var laundromat in laundromats)
        {
            var existingLaundromat = _dbContext.Laundromat.SingleOrDefault(l => l.kId == laundromat.kId);
            if (existingLaundromat == null)
            {
                _dbContext.Laundromat.Add(laundromat);
            }
        }

        await _dbContext.SaveChangesAsync();

        return Ok("Data from API 1 updated successfully");
    }
}