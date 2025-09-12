using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller quản lý đăng ký gói dịch vụ và thanh toán
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly VUniBoxContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public SubscriptionController(VUniBoxContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Subscribes a user to a new plan, handling both free and paid plans.
        /// For paid plans, it initiates a payment record.
        /// </summary>
        /// <param name="request">The subscription plan request details.</param>
        /// <returns>A <see cref="ActionResult"/> with the plan subscription response.</returns>
        [HttpPost("subscribe")]
        public async Task<ActionResult<PlanSubscriptionResponse>> SubscribeToPlan([FromBody] SubscribePlanRequest request)
        {
            try
            {
                // Kiểm tra user tồn tại
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new PlanSubscriptionResponse
                    {
                        Success = false,
                        Message = "Không tìm thấy người dùng"
                    });
                }

                // Kiểm tra gói dịch vụ tồn tại
                var plan = await _context.Plans.FindAsync(request.PlanId);
                if (plan == null)
                {
                    return NotFound(new PlanSubscriptionResponse
                    {
                        Success = false,
                        Message = "Không tìm thấy gói dịch vụ"
                    });
                }

                // Nếu là gói FREE, không cần thanh toán
                if (plan.Price == 0)
                {
                    return await SubscribeToFreePlan(user, plan);
                }

                // Tạo subscription mới
                var subscription = new Subscriptions
                {
                    UserId = request.UserId,
                    PlanId = request.PlanId,
                    StartDate = DateOnly.FromDateTime(DateTime.Now),
                    EndDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(plan.DurationMonths ?? 1)),
                    Status = "PENDING",
                    Payment = null
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                // Tạo payment record
                var payment = new Payments
                {
                    UserId = request.UserId,
                    PlanId = request.PlanId,
                    Amount = plan.Price,
                    PaymentStatus = "PENDING",
                    CreatedAt = DateTime.Now,
                    PayOsTransactionId = Guid.NewGuid().ToString(), // Temporary ID
                    QrcodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=PAY_{subscription.SubscriptionId}_{plan.Price}"
                };

                _context.Payments.Add(payment);
                
                // Update subscription with payment reference
                subscription.PaymentId = payment.PaymentId;
                
                await _context.SaveChangesAsync();

                return Ok(new PlanSubscriptionResponse
                {
                    Success = true,
                    SubscriptionId = subscription.SubscriptionId,
                    PaymentId = payment.PaymentId,
                    PaymentUrl = $"/payment/{payment.PaymentId}",
                    QrCodeUrl = payment.QrcodeUrl,
                    Amount = payment.Amount,
                    ExpiryDate = subscription.EndDate.ToDateTime(TimeOnly.MinValue),
                    Message = $"Đăng ký gói {plan.PlanName} thành công. Vui lòng thanh toán để kích hoạt gói."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PlanSubscriptionResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng ký gói dịch vụ",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Handles the subscription process for a FREE plan.
        /// </summary>
        /// <param name="user">The user subscribing to the plan.</param>
        /// <param name="plan">The FREE plan details.</param>
        /// <returns>A <see cref="ActionResult"/> with the plan subscription response for the free plan.</returns>
        private async Task<ActionResult<PlanSubscriptionResponse>> SubscribeToFreePlan(Users user, Plans plan)
        {
            // Cập nhật thông tin user
            user.CurrentPlanId = plan.PlanId;
            user.PlanExpiryDate = null; // Gói FREE không hết hạn

            // Tạo subscription record
            var subscription = new Subscriptions
            {
                UserId = user.UserId,
                PlanId = plan.PlanId,
                StartDate = DateOnly.FromDateTime(DateTime.Now),
                EndDate = DateOnly.FromDateTime(DateTime.Now.AddYears(100)), // FREE plan có EndDate rất xa
                Status = "ACTIVE",
                PaymentId = null
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            return Ok(new PlanSubscriptionResponse
            {
                Success = true,
                SubscriptionId = subscription.SubscriptionId,
                PaymentId = null,
                PaymentUrl = string.Empty,
                QrCodeUrl = string.Empty,
                Amount = 0,
                ExpiryDate = null,
                Message = $"Đăng ký gói {plan.PlanName} thành công!"
            });
        }

        /// <summary>
        /// Retrieves the current subscription information for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="ActionResult"/> with the current subscription details.</returns>
        [HttpGet("user/{userId}/current")]
        public async Task<ActionResult<CurrentSubscriptionResponse>> GetCurrentSubscription(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.CurrentPlan)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound(new CurrentSubscriptionResponse
                    {
                        Success = false,
                        Message = "Không tìm thấy người dùng"
                    });
                }

                var currentSubscription = await _context.Subscriptions
                    .Include(s => s.Plan)
                    .Include(s => s.Payment)
                    .Where(s => s.UserId == userId && s.Status == "ACTIVE")
                    .OrderByDescending(s => s.StartDate)
                    .FirstOrDefaultAsync();

                return Ok(new CurrentSubscriptionResponse
                {
                    Success = true,
                    CurrentPlan = user.CurrentPlan != null ? new PlanDto
                    {
                        PlanId = user.CurrentPlan.PlanId,
                        PlanName = user.CurrentPlan.PlanName,
                        StorageLimitMb = user.CurrentPlan.StorageLimitMb,
                        StorageDisplay = user.CurrentPlan.StorageLimitMb < 1024 
                            ? $"{user.CurrentPlan.StorageLimitMb} MB" 
                            : $"{user.CurrentPlan.StorageLimitMb / 1024} GB",
                        CitationLimit = user.CurrentPlan.CitationLimit,
                        ChatbotLimit = user.CurrentPlan.ChatbotLimit,
                        Price = user.CurrentPlan.Price,
                        PriceDisplay = user.CurrentPlan.Price == 0 
                            ? "Miễn phí" 
                            : $"{user.CurrentPlan.Price:N0} VND/tháng"
                    } : null,
                    ExpiryDate = user.PlanExpiryDate?.ToDateTime(TimeOnly.MinValue),
                    IsActive = currentSubscription?.Status == "ACTIVE",
                    DaysRemaining = user.PlanExpiryDate.HasValue 
                        ? Math.Max(0, (user.PlanExpiryDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Now).Days)
                        : null,
                    Message = "Lấy thông tin đăng ký thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new CurrentSubscriptionResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy thông tin đăng ký",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Retrieves the subscription history for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A <see cref="ActionResult"/> with the subscription history.</returns>
        [HttpGet("user/{userId}/history")]
        public async Task<ActionResult<SubscriptionHistoryResponse>> GetSubscriptionHistory(int userId)
        {
            try
            {
                var subscriptions = await _context.Subscriptions
                    .Include(s => s.Plan)
                    .Include(s => s.Payment)
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.StartDate)
                    .Select(s => new SubscriptionHistoryItem
                    {
                        SubscriptionId = s.SubscriptionId,
                        PlanName = s.Plan.PlanName,
                        StartDate = s.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = s.EndDate.ToDateTime(TimeOnly.MinValue),
                        Status = s.Status,
                        Amount = s.Payment != null ? s.Payment.Amount : 0,
                        PaymentStatus = s.Payment != null ? s.Payment.PaymentStatus : "FREE",
                        PaidAt = s.Payment != null ? s.Payment.PaidAt : null
                    })
                    .ToListAsync();

                return Ok(new SubscriptionHistoryResponse
                {
                    Success = true,
                    Subscriptions = subscriptions,
                    TotalSubscriptions = subscriptions.Count,
                    Message = "Lấy lịch sử đăng ký thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SubscriptionHistoryResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy lịch sử đăng ký",
                    Error = ex.Message
                });
            }
        }
    }
}
