using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Admin
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly VUniBoxContext _context;

        public AdminDashboardService(VUniBoxContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardResponse> GetDashboardDataAsync()
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            var today = now.Date;
            var thisMonth = new DateTime(now.Year, now.Month, 1);

            var dashboard = new AdminDashboardResponse
            {
                Overview = new DashboardOverviewDto
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalDocuments = await _context.Documents.CountAsync(),
                    TotalActiveSubscriptions = await _context.Subscriptions
                        .Where(s => s.Status == "Active")
                        .CountAsync(),
                    TotalPlans = await _context.Plans.CountAsync(),
                    ActiveUsersLast30Days = await _context.Users
                        .Where(u => u.CreatedAt >= thirtyDaysAgo)
                        .CountAsync(),
                    TotalRevenue = await _context.Payments
                        .Where(p => p.PaymentStatus == "PAID")
                        .SumAsync(p => (decimal?)p.Amount) ?? 0,
                    MonthlyRevenue = await _context.Payments
                        .Where(p => p.PaymentStatus == "PAID" && p.CreatedAt >= thisMonth)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0,
                    DocumentsUploadedToday = await _context.Documents
                        .Where(d => d.CreatedAt.HasValue && d.CreatedAt.Value.Date == today)
                        .CountAsync(),
                    NewUsersToday = await _context.Users
                        .Where(u => u.CreatedAt.HasValue && u.CreatedAt.Value.Date == today)
                        .CountAsync()
                },
                UserRegistrationStats = await GetUserRegistrationStatsAsync(thirtyDaysAgo, now),
                DocumentUploadStats = await GetDocumentUploadStatsAsync(thirtyDaysAgo, now),
                PlanUsageStats = await GetPlanUsageStatsAsync(),
                RecentActivities = await GetRecentActivitiesAsync(),
                RevenueStats = await GetRevenueStatsAsync(thirtyDaysAgo, now)
            };

            return dashboard;
        }

        public async Task<List<UserRegistrationStatsDto>> GetUserRegistrationStatsAsync(DateTime fromDate, DateTime toDate)
        {
            return await _context.Users
                .Where(u => u.CreatedAt.HasValue && u.CreatedAt >= fromDate && u.CreatedAt <= toDate)
                .GroupBy(u => u.CreatedAt.Value.Date)
                .Select(g => new UserRegistrationStatsDto
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(s => s.Date)
                .ToListAsync();
        }

        public async Task<List<DocumentUploadStatsDto>> GetDocumentUploadStatsAsync(DateTime fromDate, DateTime toDate)
        {
            var query = from d in _context.Documents
                        join ds in _context.DocumentStorage on d.DocumentId equals ds.DocumentId into docStorage
                        where d.CreatedAt.HasValue && d.CreatedAt >= fromDate && d.CreatedAt <= toDate
                        group new { d, docStorage } by d.CreatedAt.Value.Date into g
                        select new DocumentUploadStatsDto
                        {
                            Date = g.Key,
                            Count = g.Count(),
                            TotalSizeBytes = g.SelectMany(x => x.docStorage).Sum(ds => (long?)ds.FileSize) ?? 0
                        };

            return await query.OrderBy(s => s.Date).ToListAsync();
        }

        public async Task<List<PlanUsageStatsDto>> GetPlanUsageStatsAsync()
        {
            var totalSubscriptions = await _context.Subscriptions.CountAsync();

            return await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.Status == "Active")
                .GroupBy(s => new { s.Plan.PlanId, s.Plan.PlanName, s.Plan.Price })
                .Select(g => new PlanUsageStatsDto
                {
                    PlanId = g.Key.PlanId,
                    PlanName = g.Key.PlanName,
                    PlanPrice = g.Key.Price,
                    ActiveSubscriptions = g.Count(),
                    TotalRevenue = (decimal)g.Sum(s => s.Plan.Price * s.Plan.DurationMonths),
                    UsagePercentage = totalSubscriptions > 0 ? (double)g.Count() / totalSubscriptions * 100 : 0
                })
                .OrderByDescending(p => p.ActiveSubscriptions)
                .ToListAsync();
        }

        public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10)
        {
            var activities = new List<RecentActivityDto>();

            // Recent user registrations
            var recentUsers = await _context.Users
                .Where(u => u.CreatedAt.HasValue)
                .OrderByDescending(u => u.CreatedAt)
                .Take(limit / 3)
                .Select(u => new RecentActivityDto
                {
                    ActivityType = "User Registration",
                    Description = "New user registered",
                    Timestamp = u.CreatedAt.Value,
                    UserId = u.UserId.ToString(),
                    UserEmail = u.Email,
                    Details = $"Role: {u.Role}, Verified: {u.IsVerified}"
                })
                .ToListAsync();

            // Recent document uploads
            var recentDocuments = await _context.Documents
                .Include(d => d.User)
                .Where(d => d.CreatedAt.HasValue)
                .OrderByDescending(d => d.CreatedAt)
                .Take(limit / 3)
                .Select(d => new RecentActivityDto
                {
                    ActivityType = "Document Upload",
                    Description = $"Document uploaded: {d.Title ?? "Unknown"}",
                    Timestamp = d.CreatedAt.Value,
                    UserId = d.UserId.ToString(),
                    UserEmail = d.User.Email,
                    Details = $"Type: {d.DocumentType}, Status: {d.Status}"
                })
                .ToListAsync();

            // Recent payments
            var recentPayments = await _context.Payments
                .Include(p => p.User)
                .Include(p => p.Plan)
                .Where(p => p.CreatedAt.HasValue)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit / 3)
                .Select(p => new RecentActivityDto
                {
                    ActivityType = "Payment",
                    Description = $"Payment for {p.Plan.PlanName}",
                    Timestamp = p.CreatedAt.Value,
                    UserId = p.UserId.ToString(),
                    UserEmail = p.User.Email,
                    Details = $"Amount: ${p.Amount}, Status: {p.PaymentStatus}"
                })
                .ToListAsync();

            activities.AddRange(recentUsers);
            activities.AddRange(recentDocuments);
            activities.AddRange(recentPayments);

            return activities.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
        }

        public async Task<List<RevenueStatsDto>> GetRevenueStatsAsync(DateTime fromDate, DateTime toDate)
        {
            return await _context.Payments
                .Where(p => p.PaymentStatus == "PAID" &&
                           p.CreatedAt.HasValue &&
                           p.CreatedAt >= fromDate &&
                           p.CreatedAt <= toDate)
                .GroupBy(p => p.CreatedAt.Value.Date)
                .Select(g => new RevenueStatsDto
                {
                    Date = g.Key,
                    Amount = g.Sum(p => p.Amount),
                    TransactionCount = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToListAsync();
        }
    }
}