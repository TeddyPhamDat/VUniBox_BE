using VUniBox.Models.DTO.Response;

namespace VUniBox.Models.DTO.Response
{
    public class AdminDashboardResponse
    {
        public DashboardOverviewDto Overview { get; set; } = new();
        public List<UserRegistrationStatsDto> UserRegistrationStats { get; set; } = new();
        public List<DocumentUploadStatsDto> DocumentUploadStats { get; set; } = new();
        public List<PlanUsageStatsDto> PlanUsageStats { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<RevenueStatsDto> RevenueStats { get; set; } = new();
    }

    public class DashboardOverviewDto
    {
        public int TotalUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalActiveSubscriptions { get; set; }
        public int TotalPlans { get; set; }
        public int ActiveUsersLast30Days { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int DocumentsUploadedToday { get; set; }
        public int NewUsersToday { get; set; }
    }

    public class UserRegistrationStatsDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class DocumentUploadStatsDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class PlanUsageStatsDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; }
        public decimal PlanPrice { get; set; }
        public int ActiveSubscriptions { get; set; }
        public decimal TotalRevenue { get; set; }
        public double UsagePercentage { get; set; }
    }

    public class RecentActivityDto
    {
        public string ActivityType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string Details { get; set; }
    }

    public class RevenueStatsDto
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }
}