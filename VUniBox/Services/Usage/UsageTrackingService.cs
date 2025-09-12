using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;

namespace VUniBox.Services.Usage
{
    /// <summary>
    /// Defines the contract for usage tracking services.
    /// </summary>
    public interface IUsageTrackingService
    {
        /// <summary>
        /// Retrieves the usage statistics for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="UsageStats"/> object containing the user's usage data.</returns>
        Task<UsageStats> GetUserUsageAsync(int userId);
        /// <summary>
        /// Updates the storage usage for a specific user based on actual file sizes.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task UpdateStorageUsageAsync(int userId);
        /// <summary>
        /// Increments the citation usage count for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task IncrementCitationUsageAsync(int userId);
        /// <summary>
        /// Increments the chatbot usage count for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task IncrementChatbotUsageAsync(int userId);
        /// <summary>
        /// Checks if a user has sufficient storage space for an additional file.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="additionalSizeBytes">The size of the additional file in bytes.</param>
        /// <returns>True if there is sufficient space, false otherwise.</returns>
        Task<bool> CheckStorageLimitAsync(int userId, long additionalSizeBytes);
        /// <summary>
        /// Retrieves storage information (used, limit, total files) for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A tuple containing used storage in MB, storage limit in MB, and total number of files.</returns>
        Task<(long usedMb, long limitMb, int totalFiles)> GetStorageInfoAsync(int userId);
    }

    /// <summary>
    /// Service for tracking and managing user usage statistics, including storage, citations, and chatbot interactions.
    /// </summary>
    public class UsageTrackingService : IUsageTrackingService
    {
        private readonly VUniBoxContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsageTrackingService"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public UsageTrackingService(VUniBoxContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves the usage statistics for a specific user, creating a new record if none exists.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="UsageStats"/> object containing the user's usage data.</returns>
        public async Task<UsageStats> GetUserUsageAsync(int userId)
        {
            var usage = await _context.UsageStats
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (usage == null)
            {
                // Create initial usage stats
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
        /// Updates the storage usage for a specific user by recalculating from actual document file sizes.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateStorageUsageAsync(int userId)
        {
            try
            {
                // Calculate actual storage used from DocumentStorage
                var totalSizeBytes = await _context.DocumentStorage
                    .Where(ds => ds.UserId == userId)
                    .SumAsync(ds => ds.FileSize ?? 0);

                var totalSizeMb = (int)(totalSizeBytes / 1024 / 1024);

                var usage = await GetUserUsageAsync(userId);
                usage.StorageUsedMb = totalSizeMb;
                usage.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating storage usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Increments the citation usage count for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task IncrementCitationUsageAsync(int userId)
        {
            try
            {
                var usage = await GetUserUsageAsync(userId);
                usage.CitationUsed = (usage.CitationUsed ?? 0) + 1;
                usage.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error incrementing citation usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Increments the chatbot usage count for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task IncrementChatbotUsageAsync(int userId)
        {
            try
            {
                var usage = await GetUserUsageAsync(userId);
                usage.ChatbotUsed = (usage.ChatbotUsed ?? 0) + 1;
                usage.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error incrementing chatbot usage: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a user has sufficient storage space for an additional file.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="additionalSizeBytes">The size of the additional file in bytes.</param>
        /// <returns>True if there is sufficient space, false otherwise.</returns>
        public async Task<bool> CheckStorageLimitAsync(int userId, long additionalSizeBytes)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.CurrentPlan)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user?.CurrentPlan == null)
                    return false;

                var usage = await GetUserUsageAsync(userId);
                var currentUsageMb = usage.StorageUsedMb ?? 0;
                var additionalMb = additionalSizeBytes / 1024 / 1024;
                var totalUsageMb = currentUsageMb + additionalMb;

                return totalUsageMb <= user.CurrentPlan.StorageLimitMb;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking storage limit: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves storage information (used, limit, total files) for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A tuple containing used storage in MB, storage limit in MB, and total number of files.</returns>
        public async Task<(long usedMb, long limitMb, int totalFiles)> GetStorageInfoAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.CurrentPlan)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var usage = await GetUserUsageAsync(userId);

                // Count total saved files
                var totalFiles = await _context.Documents
                    .CountAsync(d => d.UserId == userId && d.Status == "Saved");

                var usedMb = usage.StorageUsedMb ?? 0;
                var limitMb = user?.CurrentPlan?.StorageLimitMb ?? 100; // Default 100MB for Free

                return (usedMb, limitMb, totalFiles);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting storage info: {ex.Message}");
            }
        }
    }
}
