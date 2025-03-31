using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorTest.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankController : ControllerBase
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;

        public BankController(IDbContextFactory<YourDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;

        }

        // Get all banks with optional filtering and pagination
        [HttpGet]
        public async Task<IActionResult> GetBanks(
            [FromQuery] bool all = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool includeLaundromatCounts = true, // New parameter
            [FromQuery] bool includeLaundromats = false // New parameter to include full laundromat data
        )
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            // Build query with filters
            var query = dbContext.Bank.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(b => b.name != null && b.name.ToLower().Contains(searchTerm));
            }

            // Get total count for metadata
            var totalCount = await query.CountAsync();

            // Create query for banks with optimized laundromat counts in a single database operation
            // This avoids the N+1 query problem you're seeing
            var banksWithCountQuery =
                from b in query
                select new
                {
                    Bank = b,
                    LaundromatCount = includeLaundromatCounts
                        ? dbContext.Laundromat.Count(l => l.bId == b.bId)
                        : 0,
                };

            // Apply pagination unless "all" is requested
            var banksWithCount = all
                ? await banksWithCountQuery.OrderBy(b => b.Bank.name).ToListAsync()
                : await banksWithCountQuery
                    .OrderBy(b => b.Bank.name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            // Create the response based on the include parameters
            var banksResponse = banksWithCount
                .Select(b => new
                {
                    b.Bank.bId,
                    b.Bank.name,
                    // Fix error 1: Use int? instead of mixing int and null
                    LaundromatCount = includeLaundromatCounts ? (int?)b.LaundromatCount : null,
                    Laundromats = includeLaundromats
                        ? dbContext
                            .Laundromat.Where(l => l.bId == b.Bank.bId)
                            .Select(l => new
                            {
                                l.kId,
                                l.name,
                                l.bId,
                            })
                            .ToList()
                        : null,
                })
                .ToList();

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
                    Banks = banksResponse,
                }
            );
        }

        // Get a specific bank by ID
        [HttpGet("{bId}")]
        public async Task<IActionResult> GetBank(int bId)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var bank = await dbContext.Bank.FindAsync(bId);

            if (bank == null)
            {
                return NotFound($"Bank with ID {bId} not found");
            }

            // Count related laundromats
            var laundromatCount = await dbContext.Laundromat.CountAsync(l => l.bId == bId);

            // Create a response that avoids circular references
            var response = new
            {
                bId = bank.bId,
                name = bank.name,
                LaundromatCount = laundromatCount,
            };

            return Ok(response);
        }

        // Get a specific bank by name
        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetBankByName(string name)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Bank name is required");
            }

            var bank = await dbContext.Bank.FirstOrDefaultAsync(b => b.name == name);

            if (bank == null)
            {
                return NotFound($"Bank with name '{name}' not found");
            }

            // Count related laundromats
            var laundromatCount = await dbContext.Laundromat.CountAsync(l => l.bId == bank.bId);

            // Create a response that avoids circular references
            var response = new
            {
                bId = bank.bId,
                name = bank.name,
                LaundromatCount = laundromatCount,
            };

            return Ok(response);
        }
    }
}
