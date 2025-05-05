using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorTest.Services
{
    public class LaundromatService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly ExternalApiService _externalApiService;
        private readonly ILogger<LaundromatService> _logger;
        private readonly string _locationsApiUrl;

        public LaundromatService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            ExternalApiService externalApiService,
            ILogger<LaundromatService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _externalApiService = externalApiService;
            _logger = logger;
            _locationsApiUrl = Environment.GetEnvironmentVariable("API_LOCATIONS");
        }

        public async Task<List<Laundromat>> GetAllLaundromatsAsync()
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Laundromat.AsNoTracking().ToListAsync();
        }

        public async Task<Laundromat> GetLaundromatByIdAsync(string kId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Laundromat.AsNoTracking()
                .FirstOrDefaultAsync(l => l.kId == kId);
        }

        public async Task<List<Laundromat>> GetLaundromatsByBankIdAsync(int bankId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Laundromat.AsNoTracking()
                .Where(l => l.bankId == bankId)
                .ToListAsync();
        }

        public async Task<int> UpdateAllLaundromatsAsync()
        {
            _logger.LogInformation("Starting laundromat and bank update");
            
            if (string.IsNullOrEmpty(_locationsApiUrl))
            {
                _logger.LogError("API_LOCATIONS environment variable is not set");
                return 0;
            }

            string baseEndpoint = _locationsApiUrl;
            (string baseUrl, int initialPageNumber) = ParseEndpoint(baseEndpoint);

            int totalLaundromats = await FetchAndProcessAllLaundromatPagesAsync(baseUrl, initialPageNumber);

            _logger.LogInformation($"Laundromat update completed. Added/updated {totalLaundromats} laundromats.");
            return totalLaundromats;
        }
        
        private async Task<int> FetchAndProcessAllLaundromatPagesAsync(string baseUrl, int startPage)
        {
            int totalLaundromats = 0;
            bool hasMoreData = true;
            int pageNumber = startPage;

            // Continue fetching pages until no more data is returned or limit is reached
            while (hasMoreData && pageNumber < 100)
            {
                // Use the same URL structure as the controller
                string currentEndpoint = $"{baseUrl}/{pageNumber}";
                _logger.LogInformation($"Fetching laundromats page {pageNumber}");
                
                var data = await _externalApiService.FetchDataAsync(currentEndpoint);

                if (string.IsNullOrEmpty(data))
                {
                    _logger.LogWarning($"No data returned for page {pageNumber}");
                    hasMoreData = false;
                    continue;
                }

                // The controller expects a direct array here, not a wrapped object
                var laundromats = JsonConvert.DeserializeObject<List<Laundromat>>(data);
                if (laundromats == null || laundromats.Count == 0)
                {
                    _logger.LogInformation($"No laundromats found on page {pageNumber}");
                    hasMoreData = false;
                    continue;
                }
                
                int addedOnThisPage = await ProcessLaundromatsAndBanksAsync(laundromats);
                totalLaundromats += addedOnThisPage;
                
                _logger.LogInformation($"Processed page {pageNumber}: {addedOnThisPage} laundromats added/updated");

                // Increment page number for next iteration
                pageNumber++;
            }
            
            return totalLaundromats;
        }

        private async Task<int> ProcessLaundromatsAndBanksAsync(List<Laundromat> laundromats)
        {
            if (laundromats == null || !laundromats.Any())
            {
                return 0;
            }
            
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            int count = 0;
            
            // Process laundromats in batches to avoid memory issues
            const int batchSize = 50;
            for (int i = 0; i < laundromats.Count; i += batchSize)
            {
                var batch = laundromats.Skip(i).Take(batchSize).ToList();
                
                foreach (var laundromatFromApi in batch)
                {
                    try
                    {
                        // First process the bank
                        if (string.IsNullOrEmpty(laundromatFromApi.bank) || laundromatFromApi.bankId == 0)
                        {
                            _logger.LogWarning($"Skipping laundromat {laundromatFromApi.kId} with missing bank information");
                            continue;
                        }
                        
                        // Get or create bank
                        var bank = await dbContext.Bank
                            .FirstOrDefaultAsync(b => b.bankId == laundromatFromApi.bankId);
                            
                        if (bank == null)
                        {
                            // Create new bank
                            bank = new BankEntity
                            {
                                bankId = laundromatFromApi.bankId,
                                name = laundromatFromApi.bank
                            };
                            
                            dbContext.Bank.Add(bank);
                            await dbContext.SaveChangesAsync(); // Save immediately to ensure we have bank ID
                        }
                        
                        // Now process the laundromat
                        var existingLaundromat = await dbContext.Laundromat
                            .FirstOrDefaultAsync(l => l.kId == laundromatFromApi.kId);
                            
                        if (existingLaundromat == null)
                        {
                            // Create a new laundromat
                            var newLaundromat = new Laundromat
                            {
                                kId = laundromatFromApi.kId,
                                externalId = laundromatFromApi.externalId,
                                bank = laundromatFromApi.bank,
                                name = laundromatFromApi.name,
                                zip = laundromatFromApi.zip,
                                longitude = laundromatFromApi.longitude,
                                latitude = laundromatFromApi.latitude,
                                bankId = bank.bankId,
                                locationId = laundromatFromApi.locationId,
                                lastFetchDate = null  // Start with no fetch date
                            };
                            
                            dbContext.Laundromat.Add(newLaundromat);
                            count++;
                        }
                        else
                        {
                            // Update existing laundromat
                            existingLaundromat.name = laundromatFromApi.name;
                            existingLaundromat.externalId = laundromatFromApi.externalId;
                            existingLaundromat.bank = laundromatFromApi.bank;
                            existingLaundromat.zip = laundromatFromApi.zip;
                            existingLaundromat.locationId = laundromatFromApi.locationId;
                            existingLaundromat.longitude = laundromatFromApi.longitude;
                            existingLaundromat.latitude = laundromatFromApi.latitude;
                            existingLaundromat.bankId = bank.bankId;
                            
                            dbContext.Entry(existingLaundromat).State = EntityState.Modified;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing laundromat {laundromatFromApi.kId}");
                    }
                }
                
                // Save changes for this batch
                await dbContext.SaveChangesAsync();
            }
            
            return count;
        }

        private async Task<BankEntity> EnsureBankExistsAsync(YourDbContext dbContext, BankEntity bankFromApi)
        {
            if (bankFromApi == null)
            {
                throw new ArgumentNullException(nameof(bankFromApi));
            }
            
            var existingBank = await dbContext.Bank
                .FirstOrDefaultAsync(b => b.bankId == bankFromApi.bankId);
                
            if (existingBank == null)
            {
                // Add new bank
                dbContext.Bank.Add(bankFromApi);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"Added new bank: {bankFromApi.name} (ID: {bankFromApi.bankId})");
                return bankFromApi;
            }
            else
            {
                // Update existing bank with new information
                existingBank.name = bankFromApi.name;
                
                dbContext.Entry(existingBank).State = EntityState.Modified;
                return existingBank;
            }
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

        private class ApiResponseWrapper
        {
            [JsonProperty("laundromatItems")]
            public List<Laundromat> Items { get; set; }
            
            [JsonProperty("pageInfo")]
            public PageInfo PageInfo { get; set; }
        }

        private class PageInfo
        {
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
            public int TotalCount { get; set; }
            public int TotalPages { get; set; }
        }
    }
}