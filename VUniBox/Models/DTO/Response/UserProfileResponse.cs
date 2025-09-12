namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Represents the response for a user profile request.
    /// </summary>
    public class UserProfileResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the user profile data.
        /// </summary>
        public UserProfileDto? UserProfile { get; set; }
        /// <summary>
        /// Gets or sets a message related to the response.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets any error message.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents the detailed user profile information.
    /// </summary>
    public class UserProfileDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the full name of the user.
        /// </summary>
        public string FullName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the role of the user (e.g., "User", "Admin").
        /// </summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the user profile was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        
        // Plan Information
        /// <summary>
        /// Gets or sets the name of the user's current plan.
        /// </summary>
        public string PlanName { get; set; } = "Free";
        /// <summary>
        /// Gets or sets the expiry date of the user's current plan.
        /// </summary>
        public DateTime? PlanExpiryDate { get; set; }
        
        // Usage Statistics
        /// <summary>
        /// Gets or sets the usage statistics for the user.
        /// </summary>
        public UsageStatsDto UsageStats { get; set; } = new();
    }

    /// <summary>
    /// Represents the usage statistics for a user.
    /// </summary>
    public class UsageStatsDto
    {
        // Storage Information
        /// <summary>
        /// Gets or sets the storage used by the user in megabytes.
        /// </summary>
        public long StorageUsedMb { get; set; }
        /// <summary>
        /// Gets or sets the total storage limit for the user in megabytes.
        /// </summary>
        public long StorageLimitMb { get; set; }
        /// <summary>
        /// Gets the percentage of storage used.
        /// </summary>
        public double StorageUsagePercentage => StorageLimitMb > 0 ? (double)StorageUsedMb / StorageLimitMb * 100 : 0;
        /// <summary>
        /// Gets the formatted string for storage used (e.g., "100MB").
        /// </summary>
        public string StorageUsedFormatted => $"{StorageUsedMb}MB";
        /// <summary>
        /// Gets the formatted string for storage limit (e.g., "500MB").
        /// </summary>
        public string StorageLimitFormatted => $"{StorageLimitMb}MB";
        /// <summary>
        /// Gets a combined string showing storage used and storage limit (e.g., "100MB / 500MB").
        /// </summary>
        public string StorageInfo => $"{StorageUsedFormatted} / {StorageLimitFormatted}";
        
        // File Statistics
        /// <summary>
        /// Gets or sets the total number of files uploaded by the user.
        /// </summary>
        public int TotalFiles { get; set; }
        /// <summary>
        /// Gets or sets the number of citations used by the user.
        /// </summary>
        public int CitationUsed { get; set; }
        /// <summary>
        /// Gets or sets the number of chatbot interactions used by the user.
        /// </summary>
        public int ChatbotUsed { get; set; }
        
        // Plan Limits
        /// <summary>
        /// Gets or sets the citation limit based on the user's plan.
        /// </summary>
        public int CitationLimit { get; set; }
        /// <summary>
        /// Gets or sets the chatbot interaction limit based on the user's plan.
        /// </summary>
        public int ChatbotLimit { get; set; }
        
        // Usage Percentages
        /// <summary>
        /// Gets the percentage of citation usage.
        /// </summary>
        public double CitationUsagePercentage => CitationLimit > 0 ? (double)CitationUsed / CitationLimit * 100 : 0;
        /// <summary>
        /// Gets the percentage of chatbot usage.
        /// </summary>
        public double ChatbotUsagePercentage => ChatbotLimit > 0 ? (double)ChatbotUsed / ChatbotLimit * 100 : 0;
        
        /// <summary>
        /// Gets or sets the last updated timestamp for the usage statistics.
        /// </summary>
        public DateTime? LastUpdated { get; set; }
    }
}
