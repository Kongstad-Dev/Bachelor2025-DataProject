using BlazorTest.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorTest.Services
{
    public class BankService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;

        public BankService(IDbContextFactory<YourDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<List<BankEntity>> GetAllBanksAsync()
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Bank.AsNoTracking().ToListAsync();
        }

        public async Task<BankEntity> GetBankByIdAsync(int bankId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Bank.AsNoTracking()
                .FirstOrDefaultAsync(b => b.bankId == bankId);
        }

        public async Task<List<Laundromat>> GetLaundromatsForBankAsync(int bankId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Laundromat.AsNoTracking()
                .Where(l => l.bankId == bankId)
                .ToListAsync();
        }
    }
}