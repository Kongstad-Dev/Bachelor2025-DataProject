using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorTest_Test.Tests.Helpers;
using BlazorTest.Database.entities;
using BlazorTest.Services.Analytics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace BlazorTest_Test.Tests.Services
{
    public class TempAnalysisServiceTests
    {
        [Fact]
        public async Task TempProgramFromTransactions_GroupsByTemperatureCorrectly()
        {
            // Arrange
            var laundromatIds = new List<string> { "1" };
            var transactions = new List<TransactionEntity>
            {
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 60, date = DateTime.Today },
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 60, date = DateTime.Today },
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 90, date = DateTime.Today },
            };

            var dbContext = TestHelpers.CreateMockDbContextWithTransactions(transactions);
            var factoryMock = TestHelpers.CreateFactoryMock(dbContext);

            var service = new TempAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.TempProgramFromTransactions(laundromatIds, DateTime.Today, DateTime.Today);

            // Assert
            result.Should().ContainSingle(d => d.Label == "90°C" && d.Value == 1);
            result.Should().ContainSingle(d => d.Label == "60°C" && d.Value == 2);
        }

        [Fact]
        public async Task TempProgramProcentageFromTransactions_ReturnsEmptyList_WhenNoMatches()
        {
            // Arrange
            var laundromatIds = new List<string> { "1" };
            var dbContext = TestHelpers.CreateMockDbContextWithTransactions(new List<TransactionEntity>());
            var factoryMock = TestHelpers.CreateFactoryMock(dbContext);
            var service = new TempAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.TempProgramProcentageFromTransactions(laundromatIds, DateTime.Today, DateTime.Today);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task TempProgramProcentageFromTransactions_ReturnsCorrectPercentage()
        {
            // Arrange
            var laundromatIds = new List<string> { "1" };
            var transactions = new List<TransactionEntity>
            {
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 60, date = DateTime.Today },
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 90, date = DateTime.Today },
                new() {kId = Guid.NewGuid().ToString(), LaundromatId = "1", temperature = 60, date = DateTime.Today },
            };

            var dbContext = TestHelpers.CreateMockDbContextWithTransactions(transactions);
            var factoryMock = TestHelpers.CreateFactoryMock(dbContext);
            var service = new TempAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.TempProgramProcentageFromTransactions(laundromatIds, DateTime.Today, DateTime.Today);

            // Assert
            result.First(d => d.Label == "60°C").Value.Should().BeApproximately(66.67m, 0.01m);
            result.First(d => d.Label == "90°C").Value.Should().BeApproximately(33.33m, 0.01m);
        }
    }
}
