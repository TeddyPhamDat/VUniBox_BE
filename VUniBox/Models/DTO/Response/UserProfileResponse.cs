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
        /// Gets or sets the phone number of the user.
        /// </summary>
        public string? PhoneNumber { get; set; }
        /// <summary>
        /// Gets or sets the avatar URL of the user.
        /// </summary>
        public string? AvatarUrl { get; set; }
        /// <summary>
        /// Gets or sets the role of the user (e.g., "User", "Admin").
        /// </summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the user profile was created.
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        
        // Account Status
        /// <summary>
        /// Gets or sets whether the user's email is verified.
        /// </summary>
        public bool IsVerified { get; set; }
        /// <summary>
        /// Gets or sets whether the user account is active.
        /// </summary>
        public bool IsActive { get; set; }
        /// <summary>
        /// Gets the account status description.
        /// </summary>
        public string AccountStatus => IsActive ? (IsVerified ? "Đã xác thực" : "Chưa xác thực") : "Bị khóa";
        
        // Plan Information
        /// <summary>
        /// Gets or sets the current plan ID.
        /// </summary>
        public int? CurrentPlanId { get; set; }
        /// <summary>
        /// Gets or sets the name of the user's current plan.
        /// </summary>
        public string PlanName { get; set; } = "Free";
        /// <summary>
        /// Gets or sets the expiry date of the user's current plan.
        /// </summary>
        public DateTime? PlanExpiryDate { get; set; }
        /// <summary>
        /// Gets whether the plan is expired.
        /// </summary>
        public bool IsPlanExpired => PlanExpiryDate.HasValue && PlanExpiryDate.Value < DateTime.Now;
        /// <summary>
        /// Gets the number of days until plan expires (or days since expired if negative).
        /// </summary>
        public int? DaysUntilExpiry => PlanExpiryDate.HasValue ? (int)(PlanExpiryDate.Value - DateTime.Now).TotalDays : null;
        
        // Document Statistics
        /// <summary>
        /// Gets or sets the total number of documents uploaded.
        /// </summary>
        public int TotalDocuments { get; set; }
        /// <summary>
        /// Gets or sets the number of documents in saved status.
        /// </summary>
        public int SavedDocuments { get; set; }
        /// <summary>
        /// Gets or sets the number of documents in trash.
        /// </summary>
        public int TrashDocuments { get; set; }
        /// <summary>
        /// Gets or sets the total number of citations generated.
        /// </summary>
        public int TotalCitations { get; set; }
        
        // Usage Statistics
        /// <summary>
        /// Gets or sets the usage statistics for the user.
        /// </summary>
        public UsageStatsDto UsageStats { get; set; } = new();
        
        // Activity Summary
        /// <summary>
        /// Gets or sets recent activity summary.
        /// </summary>
        public ActivitySummaryDto ActivitySummary { get; set; } = new();
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
        
        // Remaining Usage
        /// <summary>
        /// Gets the remaining citation count.
        /// </summary>
        public int CitationRemaining => Math.Max(0, CitationLimit - CitationUsed);
        /// <summary>
        /// Gets the remaining chatbot interactions.
        /// </summary>
        public int ChatbotRemaining => Math.Max(0, ChatbotLimit - ChatbotUsed);
        /// <summary>
        /// Gets the remaining storage in MB.
        /// </summary>
        public long StorageRemainingMb => Math.Max(0, StorageLimitMb - StorageUsedMb);
        
        // Status Indicators
        /// <summary>
        /// Gets whether storage is near limit (>80%).
        /// </summary>
        public bool IsStorageNearLimit => StorageUsagePercentage > 80;
        /// <summary>
        /// Gets whether citation usage is near limit (>80%).
        /// </summary>
        public bool IsCitationNearLimit => CitationUsagePercentage > 80;
        /// <summary>
        /// Gets whether chatbot usage is near limit (>80%).
        /// </summary>
        public bool IsChatbotNearLimit => ChatbotUsagePercentage > 80;
    }

    /// <summary>
    /// Represents activity summary information.
    /// </summary>
    public class ActivitySummaryDto
    {
        /// <summary>
        /// Gets or sets the number of documents uploaded this month.
        /// </summary>
        public int DocumentsThisMonth { get; set; }
        /// <summary>
        /// Gets or sets the number of citations generated this month.
        /// </summary>
        public int CitationsThisMonth { get; set; }
        /// <summary>
        /// Gets or sets the number of chatbot interactions this month.
        /// </summary>
        public int ChatbotThisMonth { get; set; }
        /// <summary>
        /// Gets or sets the last login date.
        /// </summary>
        public DateTime? LastLoginDate { get; set; }
        /// <summary>
        /// Gets or sets the last document upload date.
        /// </summary>
        public DateTime? LastDocumentUpload { get; set; }
        /// <summary>
        /// Gets or sets the last citation generation date.
        /// </summary>
        public DateTime? LastCitationGenerated { get; set; }
        /// <summary>
        /// Gets or sets the most frequently used document type.
        /// </summary>
        public string? FavoriteDocumentType { get; set; }
        /// <summary>
        /// Gets or sets the total number of active days (days with any activity).
        /// </summary>
        public int TotalActiveDays { get; set; }
    }
}
