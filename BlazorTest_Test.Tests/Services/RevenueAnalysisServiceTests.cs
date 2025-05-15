using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorTest.Database.entities;
using BlazorTest.Services.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorTest_Test.Tests.Services
{
    public class RevenueAnalysisServiceTests
    {
        [Fact]
        public void CalculateRevenueFromTransactions_ReturnsCorrectSum()
        {
            // Arrange
            var transactions = new List<TransactionEntity>
            {
                new TransactionEntity { amount = 100 },
                new TransactionEntity { amount = -200 },
                new TransactionEntity { amount = 50 }
            };

            var dbContextFactoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var service = new RevenueAnalysisService(dbContextFactoryMock.Object, memoryCache);

            // Act
            var result = service.CalculateRevenueFromTransactions(transactions);

            // Assert
            result.Should().Be((Math.Abs(100) + Math.Abs(-200) + Math.Abs(50)) / 100m);

        }
        [Fact]
public void CalculateRevenueFromTransactions_EmptyList_ReturnsZero()
{
    // Arrange
    var transactions = new List<TransactionEntity>(); // empty
    var dbContextFactoryMock = new Mock<IDbContextFactory<YourDbContext>>();
    var memoryCache = new MemoryCache(new MemoryCacheOptions());

    var service = new RevenueAnalysisService(dbContextFactoryMock.Object, memoryCache);

    // Act
    var result = service.CalculateRevenueFromTransactions(transactions);

    // Assert
    result.Should().Be(0m);
}

[Fact]
public void CalculateRevenueFromTransactions_SingleNegativeAmount_ReturnsPositiveDecimal()
{
    // Arrange
    var transactions = new List<TransactionEntity>
    {
        new TransactionEntity { amount = -150 }
    };
    var dbContextFactoryMock = new Mock<IDbContextFactory<YourDbContext>>();
    var memoryCache = new MemoryCache(new MemoryCacheOptions());

    var service = new RevenueAnalysisService(dbContextFactoryMock.Object, memoryCache);

    // Act
    var result = service.CalculateRevenueFromTransactions(transactions);

    // Assert
    result.Should().Be(1.5m);
}

    }
    
}
