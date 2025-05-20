using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorTest.Database.entities;
using BlazorTest.Services.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using BlazorTest_Test.Tests.Helpers;

namespace BlazorTest_Test.Tests.Services
{
    public class SoapAnalysisServiceTests
    {
        [Fact]
        public void CalculateTotalSoapProgramFromTransactions_ReturnsCorrectSum()
        {
            var transactions = new List<TransactionEntity>
            {
                new() { soap = 1 },
                new() { soap = 2 },
                new() { soap = 3 }
            };

            var factoryMock = TestHelpers.CreateFactoryMock(null); // Not used
            var service = new SoapAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            var result = service.CalculateTotalSoapProgramFromTransactions(transactions);

            result.Should().Be(6);
        }

        [Fact]
        public async Task CalculateTotalSoapProgramFromTransactions_GroupsCorrectly()
        {
            var laundromatIds = new List<string> { "1" };
            var transactions = new List<TransactionEntity>
            {
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 1, date = DateTime.Today },
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 2, date = DateTime.Today },
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 1, date = DateTime.Today }
            };

            var dbContext = TestHelpers.CreateMockDbContextWithTransactions(transactions);
            var factoryMock = TestHelpers.CreateFactoryMock(dbContext);
            var service = new SoapAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.CalculateTotalSoapProgramFromTransactions(laundromatIds, DateTime.Today, DateTime.Today);

            result.Should().ContainSingle(r => r.Label == "Soap 1" && r.Value == 2);
            result.Should().ContainSingle(r => r.Label == "Soap 2" && r.Value == 1);
            result.Should().ContainSingle(r => r.Label == "Soap 3" && r.Value == 0);
        }

        [Fact]
        public async Task CalculateTotalSoapProgramProcentageFromTransactions_ReturnsCorrectPercentages()
        {
            var laundromatIds = new List<string> { "1" };
            var transactions = new List<TransactionEntity>
            {
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 1, date = DateTime.Today },
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 2, date = DateTime.Today },
                new() { kId = Guid.NewGuid().ToString(), LaundromatId = "1", soap = 1, date = DateTime.Today }
            };

            var dbContext = TestHelpers.CreateMockDbContextWithTransactions(transactions);
            var factoryMock = TestHelpers.CreateFactoryMock(dbContext);
            var service = new SoapAnalysisService(factoryMock.Object, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.CalculateTotalSoapProgramProcentageFromTransactions(laundromatIds, DateTime.Today, DateTime.Today);

            result.Should().Contain(r => r.Label == "Soap 1" && r.Value == 66.67m);
            result.Should().Contain(r => r.Label == "Soap 2" && r.Value == 33.33m);
            result.Should().Contain(r => r.Label == "Soap 3" && r.Value == 0);
        }
    }
}
