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
using BlazorTest_Test.Tests.Helpers;

namespace BlazorTest_Test.Tests.Services
{
    public class MachineAnalysisServiceTests
    {
        [Fact]
public async Task GetStackedMachineStarts_ReturnsCorrectLabelsAndValues()
{
    // Arrange
    var laundromats = new List<Laundromat>
    {
        new() { kId = "1", name = "L1" },
        new() { kId = "2", name = "L2" }
    };
    var transactions = new List<TransactionEntity>
    {
        new() { kId = "t1", LaundromatId = "1", unitName = "W1", amount = 100, date = DateTime.Today },
        new() { kId = "t2", LaundromatId = "1", unitName = "W1", amount = 100, date = DateTime.Today },
        new() { kId = "t3", LaundromatId = "2", unitName = "D2", amount = 100, date = DateTime.Today }
    };

    var db = TestHelpers.CreateMockDbContextWithLaundromatsAndTransactions(laundromats, transactions);
    var factory = TestHelpers.CreateFactoryMock(db);
    var service = new MachineAnalysisService(factory.Object, new MemoryCache(new MemoryCacheOptions()));

    // Act
    var (labels, values, unitNames) = await service.getStackedMachineStarts(
        new List<string> { "1", "2" }, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

    // Assert
    labels.Should().Contain("L1").And.Contain("L2");
    unitNames.Should().Contain("W1").And.Contain("D2");
    values.Length.Should().Be(2);
}


        [Fact]
        public async Task GetStackedMachineRevenue_ReturnsCorrectValuesPerLaundromat()
        {
            // Arrange
            var laundromats = new List<Laundromat>
    {
        new() { kId = "1", name = "L1" }
    };
            var transactions = new List<TransactionEntity>
    {
        new() { kId = "t1", LaundromatId = "1", unitName = "W1", amount = 300, date = DateTime.Today },
        new() { kId = "t2", LaundromatId = "1", unitName = "W1", amount = 500, date = DateTime.Today }
    };

            var db = TestHelpers.CreateMockDbContextWithLaundromatsAndTransactions(laundromats, transactions);
            var factory = TestHelpers.CreateFactoryMock(db);
            var service = new MachineAnalysisService(factory.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var (_, values, unitNames) = await service.getStackedMachineRevenue(new List<string> { "1" }, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

            // Assert
            unitNames.Should().ContainSingle("W1");
            values[0][0].Should().Be(8); // (300 + 500) / 100
        }
        [Fact]
        public async Task GetMachineDetailsByLaundromat_EmptyTransactions_ReturnsEmptyMachineList()
        {
            // Arrange
            var laundromats = new List<Laundromat>
    {
        new() { kId = "1", name = "L1" }
    };

            var db = TestHelpers.CreateMockDbContextWithLaundromatsAndTransactions(laundromats, new List<TransactionEntity>());
            var factory = TestHelpers.CreateFactoryMock(db);
            var service = new MachineAnalysisService(factory.Object, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var result = await service.GetMachineDetailsByLaundromat(new List<string> { "1" }, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), "Revenue");

            // Assert
            result.Should().ContainKey("L1");
            result["L1"].Should().BeEmpty();
        }



    }
}
