using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bach2025_nortec.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bach2025_nortec.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankController : ControllerBase
    {
        private readonly YourDbContext _dbContext;

        public BankController(YourDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Get all banks with optional filtering and pagination
        [HttpGet]
        public async Task<IActionResult> GetBanks(
            [FromQuery] bool all = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? searchTerm = null
        )
        {
            // Build query with filters
            var query = _dbContext.Bank.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(b =>
                    b.name != null && b.name.ToLower().Contains(searchTerm)
                );
            }

            // Get total count for metadata
            var totalCount = await query.CountAsync();

            // Apply pagination unless "all" is requested
            var banks = all
                ? await query.OrderBy(b => b.name).ToListAsync()
                : await query
                    .OrderBy(b => b.name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            // Create a response that avoids circular references
            var banksResponse = banks.Select(b => new
            {
                b.bId,
                b.name,
                LaundromatCount = _dbContext.Laundromat.Count(l => l.bId == b.bId)
            }).ToList();

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
                    Banks = banksResponse
                }
            );
        }

        // Get a specific bank by ID
        [HttpGet("{bId}")]
        public async Task<IActionResult> GetBank(int bId)
        {
            var bank = await _dbContext.Bank.FindAsync(bId);

            if (bank == null)
            {
                return NotFound($"Bank with ID {bId} not found");
            }

            // Count related laundromats
            var laundromatCount = await _dbContext.Laundromat.CountAsync(l => l.bId == bId);

            // Create a response that avoids circular references
            var response = new
            {
                bId = bank.bId,
                name = bank.name,
                LaundromatCount = laundromatCount
            };

            return Ok(response);
        }

        // Get a specific bank by name
        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetBankByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Bank name is required");
            }

            var bank = await _dbContext.Bank.FirstOrDefaultAsync(b => b.name == name);

            if (bank == null)
            {
                return NotFound($"Bank with name '{name}' not found");
            }

            // Count related laundromats
            var laundromatCount = await _dbContext.Laundromat.CountAsync(l => l.bId == bank.bId);

            // Create a response that avoids circular references
            var response = new
            {
                bId = bank.bId,
                name = bank.name,
                LaundromatCount = laundromatCount
            };

            return Ok(response);
        }

    }
}