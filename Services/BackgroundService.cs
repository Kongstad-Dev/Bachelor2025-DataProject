using BlazorTest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorTest.Services
{
    public class DailyUpdateBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyUpdateBackgroundService> _logger;

        public DailyUpdateBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DailyUpdateBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily update background service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var scheduledTime = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0); // 2 AM
                    
                    // If it's already past 2 AM today, schedule for tomorrow
                    if (now > scheduledTime)
                    {
                        scheduledTime = scheduledTime.AddDays(1);
                    }

                    // Calculate delay
                    var delay = scheduledTime - now;
                    _logger.LogInformation($"Next update scheduled at {scheduledTime}, in {delay.TotalHours:F2} hours");

                    // Wait until the scheduled time
                    await Task.Delay(delay, stoppingToken);

                    // Execute the update
                    await RunUpdateAsync();
                    
                    // Wait a minute to avoid immediate re-execution
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in daily update background service");
                    // Wait 15 minutes before retry on error
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
            }

            _logger.LogInformation("Daily update background service is stopping");
        }

        private async Task RunUpdateAsync()
        {
            _logger.LogInformation("Starting daily update process at {time}", DateTime.Now);
            
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    // Update laundromats and banks
                    var laundromatService = scope.ServiceProvider.GetRequiredService<LaundromatService>();
                    var laundromatResult = await laundromatService.UpdateAllLaundromatsAsync();
                    _logger.LogInformation("Laundromat update completed: {result}", laundromatResult);

                    // Update transactions
                    var transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                    var transactionResult = await transactionService.UpdateAllTransactionsAsync();
                    _logger.LogInformation("Transaction update completed: Added {count} transactions, {failed} laundromats failed",
                        transactionResult.totalTransactions, transactionResult.failedLaundromats);
                    
                    // Update stats
                    var statsService = scope.ServiceProvider.GetRequiredService<LaundromatStatsService>();
                    await statsService.UpdateAllStatsAsync();
                    _logger.LogInformation("Stats update completed");
                    
                    _logger.LogInformation("Daily update process completed successfully at {time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during daily update process");
                }
            }
        }
    }
}