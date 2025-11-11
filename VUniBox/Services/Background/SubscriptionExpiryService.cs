using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;

namespace VUniBox.Services.Background
{
    /// <summary>
    /// Background service to check and reset expired subscriptions to FREE plan
    /// </summary>
    public class SubscriptionExpiryService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SubscriptionExpiryService> _logger;

        public SubscriptionExpiryService(
            IServiceProvider serviceProvider,
            ILogger<SubscriptionExpiryService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubscriptionExpiryService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndResetExpiredSubscriptionsAsync();

                    // Check every 6 hours
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SubscriptionExpiryService shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking expired subscriptions");

                    // Wait before retrying
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("SubscriptionExpiryService shutdown requested during error retry");
                        break;
                    }
                }
            }

            _logger.LogInformation("SubscriptionExpiryService stopped");
        }

        private async Task CheckAndResetExpiredSubscriptionsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VUniBoxContext>();

            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // Find all users with expired paid plans (PlanExpiryDate has passed and not on FREE plan)
                var expiredUsers = await context.Users
                    .Where(u => u.PlanExpiryDate.HasValue && 
                               u.PlanExpiryDate.Value < today &&
                               u.CurrentPlanId != 1) // Not already on FREE plan (assuming FREE plan ID is 1)
                    .ToListAsync();

                if (expiredUsers.Any())
                {
                    _logger.LogInformation($"Found {expiredUsers.Count} users with expired subscriptions");

                    foreach (var user in expiredUsers)
                    {
                        // Reset to FREE plan
                        user.CurrentPlanId = 1; // FREE plan ID
                        user.PlanExpiryDate = null; // Clear expiry date

                        _logger.LogInformation($"Reset user {user.UserId} ({user.Email}) to FREE plan (expired on {user.PlanExpiryDate})");
                    }

                    // Mark expired subscriptions as "Expired" status
                    var expiredSubscriptions = await context.Subscriptions
                        .Where(s => s.EndDate < today && s.Status == "Active")
                        .ToListAsync();

                    foreach (var subscription in expiredSubscriptions)
                    {
                        subscription.Status = "Expired";
                        _logger.LogInformation($"Marked subscription {subscription.SubscriptionId} as Expired");
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully reset {expiredUsers.Count} expired subscriptions to FREE plan");
                }
                else
                {
                    _logger.LogDebug("No expired subscriptions found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resetting expired subscriptions");
                throw;
            }
        }
    }
}
