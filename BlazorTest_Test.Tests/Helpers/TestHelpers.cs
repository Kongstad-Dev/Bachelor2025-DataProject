using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using BlazorTest.Database.entities;
using Moq;

namespace BlazorTest_Test.Tests.Helpers
{
    public static class TestHelpers
    {
        public static YourDbContext CreateMockDbContextWithTransactions(List<TransactionEntity> transactions)
        {
            var options = new DbContextOptionsBuilder<YourDbContext>()
                .UseInMemoryDatabase($"TestDb_{System.Guid.NewGuid()}")
                .Options;

            var dbContext = new YourDbContext(options);
            dbContext.Transactions.AddRange(transactions);
            dbContext.SaveChanges();

            return dbContext;
        }

        public static YourDbContext CreateMockDbContextWithLaundromatsAndTransactions(
            List<Laundromat> laundromats,
            List<TransactionEntity> transactions)
        {
            var options = new DbContextOptionsBuilder<YourDbContext>()
                .UseInMemoryDatabase($"TestDb_{System.Guid.NewGuid()}")
                .Options;

            var dbContext = new YourDbContext(options);
            dbContext.Laundromat.AddRange(laundromats);
            dbContext.Transactions.AddRange(transactions);
            dbContext.SaveChanges();

            return dbContext;
        }

        public static Mock<IDbContextFactory<YourDbContext>> CreateFactoryMock(YourDbContext context)
        {
            var factoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            factoryMock.Setup(f => f.CreateDbContext()).Returns(context);
            return factoryMock;
        }
    }
}
