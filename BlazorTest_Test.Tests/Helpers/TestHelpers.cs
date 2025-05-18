using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using BlazorTest.Database.entities;
using Moq;

namespace BlazorTest_Test.Tests.Helpers
{
    public static class TestHelpers
    {
        public static YourDbContext CreateMockDbContextWithTransactions(List<TransactionEntity> transactions)
        {
            var options = new DbContextOptionsBuilder<YourDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{System.Guid.NewGuid()}")
                .Options;

            var dbContext = new YourDbContext(options);
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
