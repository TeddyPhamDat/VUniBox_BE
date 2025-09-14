using VUniBox.Models;

namespace VUniBox.Services.Quota
{
    /// <summary>
    /// Interface for managing user quota and usage limits based on subscription plans
    /// </summary>
    public interface IQuotaManagementService
    {
        /// <summary>
        /// Check if user can perform citation action (within citation limit)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if within limit, false otherwise</returns>
        Task<bool> CanUseCitationAsync(int userId);

        /// <summary>
        /// Check if user can use chatbot (within chatbot limit)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if within limit, false otherwise</returns>
        Task<bool> CanUseChatbotAsync(int userId);

        /// <summary>
        /// Check if user can store more documents (within storage limit)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="additionalSizeMb">Additional storage size in MB</param>
        /// <returns>True if within limit, false otherwise</returns>
        Task<bool> CanStoreDocumentAsync(int userId, int additionalSizeMb = 0);

        /// <summary>
        /// Get current usage statistics for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Usage statistics with current plan limits</returns>
        Task<QuotaUsageResponse> GetUsageStatsAsync(int userId);

        /// <summary>
        /// Increment citation usage for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> IncrementCitationUsageAsync(int userId);

        /// <summary>
        /// Increment chatbot usage for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> IncrementChatbotUsageAsync(int userId);

        /// <summary>
        /// Update storage usage for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="sizeMb">Storage size in MB to add</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateStorageUsageAsync(int userId, int sizeMb);

        /// <summary>
        /// Reset monthly usage statistics (call at the beginning of each month)
        /// </summary>
        /// <param name="userId">User ID (optional, if null resets for all users)</param>
        /// <returns>Success status</returns>
        Task<bool> ResetMonthlyUsageAsync(int? userId = null);
    }

    /// <summary>
    /// Response model for quota usage information
    /// </summary>
    public class QuotaUsageResponse
    {
        public int UserId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        
        // Current usage
        public int StorageUsedMb { get; set; }
        public int CitationUsed { get; set; }
        public int ChatbotUsed { get; set; }
        
        // Plan limits
        public int StorageLimitMb { get; set; }
        public int CitationLimit { get; set; }
        public int ChatbotLimit { get; set; }
        
        // Remaining quota
        public int StorageRemainingMb => StorageLimitMb - StorageUsedMb;
        public int CitationRemaining => CitationLimit - CitationUsed;
        public int ChatbotRemaining => ChatbotLimit - ChatbotUsed;
        
        // Usage percentages
        public double StorageUsagePercent => StorageLimitMb > 0 ? (double)StorageUsedMb / StorageLimitMb * 100 : 0;
        public double CitationUsagePercent => CitationLimit > 0 ? (double)CitationUsed / CitationLimit * 100 : 0;
        public double ChatbotUsagePercent => ChatbotLimit > 0 ? (double)ChatbotUsed / ChatbotLimit * 100 : 0;
        
        public DateTime LastUpdated { get; set; }
    }
}
