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
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                    
                    // Check every hour
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while resetting monthly quota");
                    
                    // Wait before retrying
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }
    }
}
