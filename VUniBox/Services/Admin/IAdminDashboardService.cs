using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Admin
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardResponse> GetDashboardDataAsync();
        Task<List<UserRegistrationStatsDto>> GetUserRegistrationStatsAsync(DateTime fromDate, DateTime toDate);
        Task<List<DocumentUploadStatsDto>> GetDocumentUploadStatsAsync(DateTime fromDate, DateTime toDate);
        Task<List<PlanUsageStatsDto>> GetPlanUsageStatsAsync();
        Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10);
        Task<List<RevenueStatsDto>> GetRevenueStatsAsync(DateTime fromDate, DateTime toDate);
    }
}