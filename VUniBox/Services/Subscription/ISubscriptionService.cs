using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.Enum;
using Microsoft.EntityFrameworkCore;

namespace VUniBox.Services.Subscription
{
    /// <summary>
    /// Interface for managing user subscriptions
    /// </summary>
    public interface ISubscriptionService
    {
        /// <summary>
        /// Subscribe user to a plan
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="planId">Plan ID</param>
        /// <param name="paymentId">Payment ID (optional for free plan)</param>
        /// <returns>Created subscription</returns>
        Task<Subscriptions> SubscribeUserAsync(int userId, int planId, int? paymentId = null);

        /// <summary>
        /// Get user's current active subscription
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Active subscription or null</returns>
        Task<Subscriptions?> GetActiveSubscriptionAsync(int userId);
    }
}
