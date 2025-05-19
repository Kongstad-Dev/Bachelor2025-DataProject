using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorTest.Database;
using BlazorTest_Test.Tests.Helpers;
using BlazorTest.Database.entities;
using BlazorTest.Services.Analytics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace BlazorTest_Test.Tests.Analysis
{
    public class AnalysisServiceIntegrationTests
    {
        private static YourDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<YourDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new YourDbContext(options);
        }

        [Fact]
        public async Task RevenueAnalysisService_CalculatesRevenueFromStatsAndFallback()
        {
            using var dbContext = CreateInMemoryDbContext();
            var laundromatId = "L1";
          dbContext.LaundromatStats.Add(new LaundromatStats
        {
            LaundromatId = laundromatId,
            LaundromatName = "Test Laundromat",
            PeriodType = StatsPeriodType.Month,
            PeriodKey = DateTime.Today.ToString("yyyy-MM"),
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today,
            TotalRevenue = 1000,
            TotalTransactions = 10,
            WashingMachineTransactions = 5,
            WasherStartPrice = 20,
            DryerTransactions = 5,
            DryerStartPrice = 15,
            RevenueTimeSeriesData = "{}", // or valid JSON
            TransactionCountTimeSeriesData = "{}", // or valid JSON
            AvailableTimeSeriesData = TimeSeriesDataTypes.Revenue,
            CalculatedAt = DateTime.Now
        });

            dbContext.Transactions.Add(new TransactionEntity
            {
                kId = Guid.NewGuid().ToString(),
                LaundromatId = laundromatId,
                amount = 1000,
                date = DateTime.Today
            });

            await dbContext.SaveChangesAsync();

            var factory = new DbContextFactoryMock<YourDbContext>(dbContext);
            var service = new RevenueAnalysisService(factory, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.CalculateLaundromatsRevenue(
                new List<string> { laundromatId },
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            result.Should().Be(10.00m);
        }

        [Fact]
        public async Task TransactionAnalysisService_CanFallBackToRawQuery()
        {
            using var dbContext = CreateInMemoryDbContext();

            dbContext.Laundromat.Add(new Laundromat { kId = "L1", bankId = 1 });
            dbContext.Transactions.AddRange(
                new TransactionEntity { kId = Guid.NewGuid().ToString(), LaundromatId = "L1", amount = 100, date = new DateTime(2024, 3, 2) },
                new TransactionEntity { kId = Guid.NewGuid().ToString(), LaundromatId = "L1", amount = 200, date = new DateTime(2024, 3, 30) }
            );

            await dbContext.SaveChangesAsync();

            var factory = new DbContextFactoryMock<YourDbContext>(dbContext);
            var service = new TransactionAnalysisService(factory, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.CalculateTransactionOverTime(
                new List<string> { "L1" },
                new DateTime(2024, 3, 1),
                new DateTime(2024, 4, 2));
                
              foreach (var entry in result)
            {
                Console.WriteLine($"Label: {entry.Label}, Value: {entry.Value}");
            }
            result.Should().NotBeNull();
            result.Where(r => r.Value != 0).Select(r => r.Value).Should().Contain(1);
            result.Where(r => r.Value != 0).Select(r => r.Value).Should().HaveCount(2);
        }

        [Fact]
        public async Task TempAnalysisService_ResolvesTemperatureGroupsCorrectly()
        {
            using var dbContext = CreateInMemoryDbContext();

            dbContext.Transactions.AddRange(
                new TransactionEntity { kId = Guid.NewGuid().ToString(), LaundromatId = "L1", temperature = 60, date = DateTime.Today },
                new TransactionEntity { kId = Guid.NewGuid().ToString(), LaundromatId = "L1", temperature = 90, date = DateTime.Today },
                new TransactionEntity { kId = Guid.NewGuid().ToString(), LaundromatId = "L1", temperature = 60, date = DateTime.Today }
            );

            await dbContext.SaveChangesAsync();

            var factory = new DbContextFactoryMock<YourDbContext>(dbContext);
            var service = new TempAnalysisService(factory, new MemoryCache(new MemoryCacheOptions()));

            var result = await service.TempProgramProcentageFromTransactions(
                new List<string> { "L1" },
                DateTime.Today.AddDays(-1),
                DateTime.Today.AddDays(1));

            result.Should().Contain(p => p.Label == "60°C" && p.Value == 66.67m);
            result.Should().Contain(p => p.Label == "90°C" && p.Value == 33.33m);
        }
    }
}
