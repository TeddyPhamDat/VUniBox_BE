using VUniBox.Services.Quota;

namespace VUniBox.Services.Background
{
    /// <summary>
    /// Background service to reset monthly usage quotas
    /// </summary>
    public class MonthlyQuotaResetService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonthlyQuotaResetService> _logger;

        public MonthlyQuotaResetService(
            IServiceProvider serviceProvider,
            ILogger<MonthlyQuotaResetService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MonthlyQuotaResetService started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if it's the first day of the month
                    var now = DateTime.UtcNow;
                    if (now.Day == 1 && now.Hour == 0)
                    {
                        _logger.LogInformation("Starting monthly quota reset...");
                        
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaManagementService>();
                            
                            var success = await quotaService.ResetMonthlyUsageAsync();
                            
                            if (success)
                            {
                                _logger.LogInformation("Monthly quota reset completed successfully");
                            }
                            else
                            {
                                _logger.LogError("Failed to reset monthly quota");
                            }
                        }
                        
                        // Wait for the rest of the hour to avoid multiple resets
                        try
                        {
                            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogInformation("MonthlyQuotaResetService shutdown requested during hour delay");
                            break;
                        }
                    }
                    
                    // Check every hour
                    try
                    {
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("MonthlyQuotaResetService shutdown requested during hourly check");
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("MonthlyQuotaResetService shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while resetting monthly quota");
                    
                    // Wait before retrying, but handle cancellation
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("MonthlyQuotaResetService shutdown requested during error retry delay");
                        break;
                    }
                }
            }
            
            _logger.LogInformation("MonthlyQuotaResetService stopped");
        }
    }
}
