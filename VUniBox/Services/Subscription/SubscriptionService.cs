using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.Enum;
using Microsoft.EntityFrameworkCore;

namespace VUniBox.Services.Subscription
{
    /// <summary>
    /// Service for managing user subscriptions and lifecycle
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly VUniBoxContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(VUniBoxContext context, ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Subscribe user to a plan with automatic end date calculation
        /// </summary>
        public async Task<Subscriptions> SubscribeUserAsync(int userId, int planId, int? paymentId = null)
        {
            try
            {
                var plan = await _context.Plans.FindAsync(planId);
                if (plan == null)
                    throw new ArgumentException($"Plan with ID {planId} not found");

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    throw new ArgumentException($"User with ID {userId} not found");

                var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
                DateOnly endDate;

                // Calculate end date based on plan duration
                if (plan.DurationMonths.HasValue)
                {
                    // For paid plans, add the duration months
                    endDate = startDate.AddMonths(plan.DurationMonths.Value);
                }
                else
                {
                    // For FREE plan, set far future date (effectively no expiry)
                    endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(10));
                }

                var subscription = new Subscriptions
                {
                    UserId = userId,
                    PlanId = planId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Status = "Active",
                    PaymentId = paymentId
                };

                _context.Subscriptions.Add(subscription);

                // Update user's current plan
                user.CurrentPlanId = planId;
                _context.Users.Update(user);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} subscribed to plan {planId} from {startDate} to {endDate}");
                
                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error subscribing user {userId} to plan {planId}");
                throw;
            }
        }

        /// <summary>
        /// Get user's current active subscription
        /// </summary>
        public async Task<Subscriptions?> GetActiveSubscriptionAsync(int userId)
        {
            return await _context.Subscriptions
                .Include(s => s.Plan)
                .Include(s => s.Payment)
                .Where(s => s.UserId == userId && 
                           s.Status == "Active" && 
                           s.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();
        }
    }
}
