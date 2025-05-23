﻿using BlazorTest.Database;
using BlazorTest.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorTest.Services
{
    public class DataAnalysisService
    {
        private readonly IDbContextFactory<YourDbContext> _dbContextFactory;
        private readonly IMemoryCache _cache;

        public DataAnalysisService(
            IDbContextFactory<YourDbContext> dbContextFactory,
            IMemoryCache cache
        )
        {
            _dbContextFactory = dbContextFactory;
            _cache = cache;
        }

        private bool DateEquals(DateTime date1, DateTime date2)
        {
            return date1.Date == date2.Date;
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValuesFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var keyValueService = new KeyValueAnalysisService(_dbContextFactory, _cache);
            return await keyValueService.GetKeyValuesFromStats(laundromatIds, startDate, endDate);
        }

        public async Task<List<KeyValuePair<string, decimal>>> GetKeyValues(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var keyValueService = new KeyValueAnalysisService(_dbContextFactory, _cache);
            return await keyValueService.GetKeyValues(laundromatIds, startDate, endDate);
        }

        public async Task<decimal> CalculateLaundromatsRevenue(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var revenueService = new RevenueAnalysisService(_dbContextFactory, _cache);
            return await revenueService.CalculateLaundromatsRevenue(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var chartService = new ChartAnalysisService(_dbContextFactory, _cache);
            return await chartService.GetRevenueForLaundromatsFromStats(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var chartService = new ChartAnalysisService(_dbContextFactory, _cache);
            return await chartService.GetRevenueForLaundromats(laundromatIds, startDate, endDate);
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTimeFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var chartService = new ChartAnalysisService(_dbContextFactory, _cache);
            return await chartService.GetRevenueForLaundromatsOverTimeFromStats(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> GetRevenueForLaundromatsOverTime(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var chartService = new ChartAnalysisService(_dbContextFactory, _cache);
            return await chartService.GetRevenueForLaundromatsOverTime(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var soapService = new SoapAnalysisService(_dbContextFactory, _cache);
            return await soapService.CalculateTotalSoapProgramFromTransactions(
                laundromatIds,
                startDate,
                endDate
            );
        }

        //Temp Total Calculation
        public async Task<List<ChartDataPoint>> TempProgramFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var tempService = new TempAnalysisService(_dbContextFactory, _cache);
            return await tempService.TempProgramFromTransactions(laundromatIds, startDate, endDate);
        }

        //Temp Procentage
        public async Task<List<ChartDataPoint>> TempProgramProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var tempService = new TempAnalysisService(_dbContextFactory, _cache);
            return await tempService.TempProgramProcentageFromTransactions(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> ProgramTypeProgramFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var ProgramTypeAnalysisService = new ProgramTypeAnalysisService(
                _dbContextFactory,
                _cache
            );
            return await ProgramTypeAnalysisService.ProgramTypeProgramFromTransactions(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> ProgramTypeProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var ProgramTypeAnalysisService = new ProgramTypeAnalysisService(
                _dbContextFactory,
                _cache
            );
            return await ProgramTypeAnalysisService.ProgramTypeProcentageFromTransactions(
                laundromatIds,
                startDate,
                endDate
            );
        }
        
        public async Task<List<ChartDataPoint>> RinseFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var RinseAnalysisService = new RinseAnalysisService(_dbContextFactory, _cache);
            return await RinseAnalysisService.RinseFromTransactions(laundromatIds, startDate,
                endDate);
        }
        
        public async Task<List<ChartDataPoint>> RinseProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var RinseProcentageAnalysisService = new RinseAnalysisService(_dbContextFactory, _cache);
            return await RinseProcentageAnalysisService.RinseProcentageFromTransactions(laundromatIds, startDate,
                endDate);
        }

        public async Task<List<ChartDataPoint>> CalculateTransactionOverTimeFromStats(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var transactionService = new TransactionAnalysisService(_dbContextFactory, _cache);
            return await transactionService.CalculateTransactionOverTimeFromStats(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> CalculateTransactionOverTime(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var transactionService = new TransactionAnalysisService(_dbContextFactory, _cache);
            return await transactionService.CalculateTransactionOverTime(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> CalculateTotalSoapProgramProcentageFromTransactions(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var soapService = new SoapAnalysisService(_dbContextFactory, _cache);
            return await soapService.CalculateTotalSoapProgramProcentageFromTransactions(
                laundromatIds,
                startDate,
                endDate
            );
        }

        public async Task<List<ChartDataPoint>> CalculateAvgSecoundsFromTransactions(int bankId)
        {
            var transactionService = new TransactionAnalysisService(_dbContextFactory, _cache);
            return await transactionService.CalculateAvgSecoundsFromTransactions(bankId);
        }

        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
        )> getStackedMachineStarts(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var machineService = new MachineAnalysisService(_dbContextFactory, _cache);
            return await machineService.getStackedMachineStarts(laundromatIds, startDate, endDate);
        }
        
        public async Task<(
            string[] Labels,
            decimal[][] Values,
            string[] unitNames
            )> getStackedMachineRevenue(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate
        )
        {
            var machineService = new MachineAnalysisService(_dbContextFactory, _cache);
            return await machineService.getStackedMachineRevenue(laundromatIds, startDate, endDate);
        }

        public async Task<Dictionary<string, List<MachineDetailRow>>> GetMachineDetailsByLaundromat(
            List<string> laundromatIds,
            DateTime? startDate,
            DateTime? endDate,
            string metricKey)
        {
            var machineService = new MachineAnalysisService(_dbContextFactory, _cache);
            return await machineService.GetMachineDetailsByLaundromat(
                laundromatIds, 
                startDate, 
                endDate,
                metricKey);
        }

        // Legacy methods that operate on transaction lists directly
        public decimal CalculateTotalSoapProgramFromTransactions(
            List<TransactionEntity> transactions
        )
        {
            var soapService = new SoapAnalysisService(_dbContextFactory, _cache);
            return soapService.CalculateTotalSoapProgramFromTransactions(transactions);
        }

        public decimal CalculateRevenueFromTransactions(List<TransactionEntity> transactions)
        {
            var revenueService = new RevenueAnalysisService(_dbContextFactory, _cache);
            return revenueService.CalculateRevenueFromTransactions(transactions);
        }

        public decimal CalculateAvgSecoundsFromTransactions(List<TransactionEntity> transactions)
        {
            var transactionService = new TransactionAnalysisService(_dbContextFactory, _cache);
            return transactionService.CalculateAvgSecoundsFromTransactions(transactions);
        }
    }
}
