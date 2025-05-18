using System;
using System.Collections.Generic;
using BlazorTest.Database.entities;
using BlazorTest.Services.Analytics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace BlazorTest_Test.Tests.Services
{
    public class TransactionAnalysisServiceTests
    {
        private TransactionAnalysisService CreateService()
        {
            var dbContextFactoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            return new TransactionAnalysisService(dbContextFactoryMock.Object, memoryCache);
        }

        [Fact]
        public void CalculateAvgSeconds_ReturnsCorrectAverage_WhenValidTransactionsExist()
        {
            // Arrange
            var service = CreateService();
            var transactions = new List<TransactionEntity>
            {
                new TransactionEntity { seconds = 30 },
                new TransactionEntity { seconds = 90 },
                new TransactionEntity { seconds = 60 }
            };

            // Act
            var result = service.CalculateAvgSecoundsFromTransactions(transactions);

            // Assert
            result.Should().Be(1); // (30+90+60)/3 = 60 => 1 min
        }

        [Fact]
        public void CalculateAvgSeconds_ReturnsZero_WhenNoTransactionsWithPositiveSeconds()
        {
            // Arrange
            var service = CreateService();
            var transactions = new List<TransactionEntity>
            {
                new TransactionEntity { seconds = 0 },
                new TransactionEntity { seconds = -1 }
            };

            // Act
            var result = service.CalculateAvgSecoundsFromTransactions(transactions);

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void CalculateAvgSeconds_ReturnsZero_WhenListIsEmpty()
        {
            // Arrange
            var service = CreateService();
            var transactions = new List<TransactionEntity>();

            // Act
            var result = service.CalculateAvgSecoundsFromTransactions(transactions);

            // Assert
            result.Should().Be(0);
        }
    }
}
