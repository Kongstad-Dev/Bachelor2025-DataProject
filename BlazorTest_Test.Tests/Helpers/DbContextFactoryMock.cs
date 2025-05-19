using Microsoft.EntityFrameworkCore;

namespace BlazorTest_Test.Tests.Analysis
{
    public class DbContextFactoryMock<TContext> : IDbContextFactory<TContext> where TContext : DbContext
    {
        private readonly TContext _context;

        public DbContextFactoryMock(TContext context)
        {
            _context = context;
        }

        public TContext CreateDbContext()
        {
            return _context;
        }
    }
}