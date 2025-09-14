using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;

namespace VUniBox.Services.Quota
{
    /// <summary>
    /// Service for managing user quota and usage limits based on subscription plans
    /// </summary>
    public class QuotaManagementService : IQuotaManagementService
    {
        private readonly VUniBoxContext _context;

        public QuotaManagementService(VUniBoxContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Check if user can perform citation action (within citation limit)
        /// </summary>
        public async Task<bool> CanUseCitationAsync(int userId)
        {
            var usage = await GetOrCreateUsageStatsAsync(userId);
            var user = await GetUserWithPlanAsync(userId);
            
            if (user?.CurrentPlan == null)
                return false; // No plan = no access

            return (usage.CitationUsed ?? 0) < user.CurrentPlan.CitationLimit;
        }

        /// <summary>
        /// Check if user can use chatbot (within chatbot limit)
        /// </summary>
        public async Task<bool> CanUseChatbotAsync(int userId)
        {
            var usage = await GetOrCreateUsageStatsAsync(userId);
            var user = await GetUserWithPlanAsync(userId);
            
            if (user?.CurrentPlan == null)
                return false; // No plan = no access

            return (usage.ChatbotUsed ?? 0) < user.CurrentPlan.ChatbotLimit;
        }

        /// <summary>
        /// Check if user can store more documents (within storage limit)
        /// </summary>
        public async Task<bool> CanStoreDocumentAsync(int userId, int additionalSizeMb = 0)
        {
            var usage = await GetOrCreateUsageStatsAsync(userId);
            var user = await GetUserWithPlanAsync(userId);
            
            if (user?.CurrentPlan == null)
                return false; // No plan = no access

            var totalUsage = (usage.StorageUsedMb ?? 0) + additionalSizeMb;
            return totalUsage <= user.CurrentPlan.StorageLimitMb;
        }

        /// <summary>
        /// Get current usage statistics for a user
        /// </summary>
        public async Task<QuotaUsageResponse> GetUsageStatsAsync(int userId)
        {
            var usage = await GetOrCreateUsageStatsAsync(userId);
            var user = await GetUserWithPlanAsync(userId);
            
            if (user?.CurrentPlan == null)
            {
                throw new InvalidOperationException($"User {userId} does not have an active plan");
            }

            return new QuotaUsageResponse
            {
                UserId = userId,
                PlanName = user.CurrentPlan.PlanName,
                
                // Current usage
                StorageUsedMb = usage.StorageUsedMb ?? 0,
                CitationUsed = usage.CitationUsed ?? 0,
                ChatbotUsed = usage.ChatbotUsed ?? 0,
                
                // Plan limits
                StorageLimitMb = user.CurrentPlan.StorageLimitMb,
                CitationLimit = user.CurrentPlan.CitationLimit,
                ChatbotLimit = user.CurrentPlan.ChatbotLimit,
                
                LastUpdated = usage.LastUpdated ?? DateTime.UtcNow
            };
        }

        /// <summary>
        /// Increment citation usage for a user
        /// </summary>
        public async Task<bool> IncrementCitationUsageAsync(int userId)
        {
            try
            {
                var usage = await GetOrCreateUsageStatsAsync(userId);
                usage.CitationUsed = (usage.CitationUsed ?? 0) + 1;
                usage.LastUpdated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing citation usage: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Increment chatbot usage for a user
        /// </summary>
        public async Task<bool> IncrementChatbotUsageAsync(int userId)
        {
            try
            {
                var usage = await GetOrCreateUsageStatsAsync(userId);
                usage.ChatbotUsed = (usage.ChatbotUsed ?? 0) + 1;
                usage.LastUpdated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing chatbot usage: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update storage usage for a user
        /// </summary>
        public async Task<bool> UpdateStorageUsageAsync(int userId, int sizeMb)
        {
            try
            {
                var usage = await GetOrCreateUsageStatsAsync(userId);
                usage.StorageUsedMb = (usage.StorageUsedMb ?? 0) + sizeMb;
                usage.LastUpdated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating storage usage: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reset monthly usage statistics (call at the beginning of each month)
        /// </summary>
        public async Task<bool> ResetMonthlyUsageAsync(int? userId = null)
        {
            try
            {
                var query = _context.UsageStats.AsQueryable();
                
                if (userId.HasValue)
                {
                    query = query.Where(u => u.UserId == userId.Value);
                }

                var usageStats = await query.ToListAsync();
                
                foreach (var usage in usageStats)
                {
                    // Only reset citation and chatbot usage, keep storage usage
                    usage.CitationUsed = 0;
                    usage.ChatbotUsed = 0;
                    usage.LastUpdated = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting monthly usage: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get or create usage statistics for a user
        /// </summary>
        private async Task<UsageStats> GetOrCreateUsageStatsAsync(int userId)
        {
            var usage = await _context.UsageStats
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (usage == null)
            {
                usage = new UsageStats
                {
                    UserId = userId,
                    StorageUsedMb = 0,
                    CitationUsed = 0,
                    ChatbotUsed = 0,
                    LastUpdated = DateTime.UtcNow
                };
                
                _context.UsageStats.Add(usage);
                await _context.SaveChangesAsync();
            }

            return usage;
        }

        /// <summary>
        /// Get user with current plan information
        /// </summary>
        private async Task<Users?> GetUserWithPlanAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.CurrentPlan)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
