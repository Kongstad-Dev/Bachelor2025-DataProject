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

        [Fact]
        public async Task CalculateLaundromatsRevenue_ReturnsZero_WhenLaundromatIdsEmpty()
        {
            // Arrange
            var factoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            var service = new RevenueAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.CalculateLaundromatsRevenue(new List<string>(), DateTime.Today, DateTime.Today);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateLaundromatsRevenue_ReturnsZero_WhenDatesAreNull()
        {
            // Arrange
            var factoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            var service = new RevenueAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.CalculateLaundromatsRevenue(new List<string> { "1" }, null, DateTime.Today);

            // Assert
            result.Should().Be(0m);
        }

        [Fact]
        public async Task CalculateLaundromatsRevenue_ReturnsFromCache_IfExists()
        {
            // Arrange
            var factoryMock = new Mock<IDbContextFactory<YourDbContext>>();
            var cache = new MemoryCache(new MemoryCacheOptions());

            var service = new RevenueAnalysisService(factoryMock.Object, cache);
            var laundromatIds = new List<string> { "1" };
            var start = new DateTime(2024, 1, 1);
            var end = new DateTime(2024, 1, 31);
            var key = $"revenue_1_20240101_20240131";

            cache.Set(key, 12.34m);

            // Act
            var result = await service.CalculateLaundromatsRevenue(laundromatIds, start, end);

            // Assert
            result.Should().Be(12.34m);
        }
    }
}
