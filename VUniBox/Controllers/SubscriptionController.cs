using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;
using VUniBox.Services.Subscription;
using VUniBox.Services.Payment;

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
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPayOSService _payOSService;
        private readonly ILogger<SubscriptionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="subscriptionService">The subscription service.</param>
        /// <param name="payOSService">The PayOS service.</param>
        /// <param name="logger">The logger.</param>
        public SubscriptionController(VUniBoxContext context, ISubscriptionService subscriptionService, IPayOSService payOSService, ILogger<SubscriptionController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _payOSService = payOSService;
            _logger = logger;
        }

        /// <summary>
        /// Handles the subscription process for a FREE plan.
        /// </summary>
        /// <param name="user">The user subscribing to the plan.</param>
        /// <param name="plan">The FREE plan details.</param>
        /// <returns>A <see cref="ActionResult"/> with the plan subscription response for the free plan.</returns>
        private async Task<ActionResult<PlanSubscriptionResponse>> SubscribeToFreePlan(Users user, Plans plan)
        {
            // Check if user already has an active subscription
            var existingSubscription = await _context.Subscriptions
                .Where(s => s.UserId == user.UserId && s.Status == "ACTIVE")
                .FirstOrDefaultAsync();

            if (existingSubscription != null)
            {
                return Ok(new PlanSubscriptionResponse
                {
                    Success = true,
                    SubscriptionId = existingSubscription.SubscriptionId,
                    PaymentId = null,
                    PaymentUrl = string.Empty,
                    QrCodeUrl = string.Empty,
                    Amount = 0,
                    ExpiryDate = null,
                    Message = $"Bạn đã có gói {plan.PlanName} đang hoạt động!"
                });
            }

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
        /// Creates a PayOS payment for subscription.
        /// </summary>
        /// <param name="request">The PayOS payment request.</param>
        /// <returns>A <see cref="ActionResult"/> with the PayOS payment response.</returns>
        [HttpPost("payos")]
        public async Task<ActionResult<PlanSubscriptionResponse>> CreatePayOSPayment([FromBody] SubscribePlanRequest request)
        {
            try
            {
                // Validate user exists
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return NotFound(new PlanSubscriptionResponse
                    {
                        Success = false,
                        Message = "Người dùng không tồn tại"
                    });
                }

                // Validate plan exists
                var plan = await _context.Plans.FindAsync(request.PlanId);
                if (plan == null)
                {
                    return NotFound(new PlanSubscriptionResponse
                    {
                        Success = false,
                        Message = "Gói dịch vụ không tồn tại"
                    });
                }

                // Handle FREE plan
                if (plan.Price == 0)
                {
                    return await SubscribeToFreePlan(user, plan);
                }

                // Create payment record first
                var payment = new Payments
                {
                    UserId = request.UserId,
                    PlanId = request.PlanId,
                    Amount = plan.Price,
                    PaymentStatus = "Pending", // Use correct constraint value
                    PayOsTransactionId = "", // Will be set after creating orderCode
                    CreatedAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Create PayOS payment with proper OrderCode
                var paymentOrderCode = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss")); // Use timestamp as orderCode
                payment.PayOsTransactionId = paymentOrderCode.ToString(); // Update payment record
                
                var paymentRequest = new PayOSPaymentRequest
                {
                    OrderCode = paymentOrderCode,
                    Amount = (int)plan.Price,
                    Description = $"Thanh toán gói {plan.PlanName}",
                    BuyerName = user.FullName ?? "VUniBox User",
                    BuyerEmail = user.Email ?? "",
                    BuyerPhone = "",
                    BuyerAddress = ""
                };

                var paymentResponse = await _payOSService.CreatePaymentAsync(paymentRequest);

                if (paymentResponse == null || !paymentResponse.Success || string.IsNullOrEmpty(paymentResponse.CheckoutUrl))
                {
                    return BadRequest(new PlanSubscriptionResponse
                    {
                        Success = false,
                        Message = paymentResponse?.Message ?? "Không thể tạo liên kết thanh toán PayOS"
                    });
                }

                // Update payment with PayOS QR code URL
                payment.QrcodeUrl = paymentResponse.QrCode;
                await _context.SaveChangesAsync();

                return Ok(new PlanSubscriptionResponse
                {
                    Success = true,
                    SubscriptionId = 0, // Will be created after successful payment
                    PaymentId = payment.PaymentId,
                    PaymentUrl = paymentResponse.CheckoutUrl,
                    QrCodeUrl = paymentResponse.QrCode,
                    Amount = plan.Price,
                    ExpiryDate = DateTime.UtcNow.AddMinutes(15),
                    Message = $"Đã tạo liên kết thanh toán PayOS cho gói {plan.PlanName}. Vui lòng thanh toán để kích hoạt gói.",
                    PayOsTransactionId = paymentResponse.OrderCode.ToString(),
                    OrderCode = paymentResponse.OrderCode // Add OrderCode to response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating PayOS payment for user {request.UserId}, plan {request.PlanId}");
                return StatusCode(500, new PlanSubscriptionResponse
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi tạo thanh toán PayOS"
                });
            }
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

        
        /// <summary>
        /// Check PayOS payment status
        /// </summary>
        /// <param name="orderCode">The order code to check</param>
        /// <returns>The payment status</returns>
        [HttpGet("payos/check/{orderCode}")]
        public async Task<ActionResult> CheckPayOSPaymentStatus(string orderCode)
        {
            try
            {
                // Find payment by PayOsTransactionId (which stores the orderCode)
                var payment = await _context.Payments
                    .Include(p => p.Plan)
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.PayOsTransactionId == orderCode);

                if (payment == null)
                {
                    return NotFound(new { 
                        Success = false,
                        Message = "Không tìm thấy thanh toán với mã này",
                        OrderCode = orderCode
                    });
                }

                // Convert orderCode to long for PayOS API
                if (!long.TryParse(orderCode, out long orderCodeLong))
                {
                    return BadRequest(new {
                        Success = false,
                        Message = "Mã đơn hàng không hợp lệ",
                        OrderCode = orderCode
                    });
                }

                // Get payment info from PayOS
                var paymentInfo = await _payOSService.GetPaymentLinkInfoAsync(orderCodeLong);
                
                if (paymentInfo != null)
                {
                    // Update payment status based on PayOS response
                    if (paymentInfo.status == "PAID" && payment.PaymentStatus != "Paid")
                    {
                        // Update payment status
                        payment.PaymentStatus = "Paid"; // Use correct constraint value
                        payment.PaidAt = DateTime.UtcNow;
                        _context.Payments.Update(payment);

                        // Create subscription after successful payment
                        var subscriptionResult = await _subscriptionService.SubscribeUserAsync(payment.UserId, payment.PlanId, payment.PaymentId);
                        
                        // Update user's current plan (CRITICAL: This was missing!)
                        payment.User.CurrentPlanId = payment.PlanId;
                        payment.User.PlanExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(payment.Plan?.DurationMonths ?? 1));
                        _context.Users.Update(payment.User);
                        
                        await _context.SaveChangesAsync();

                        return Ok(new {
                            Success = true,
                            PaymentStatus = payment.PaymentStatus,
                            SubscriptionCreated = true,
                            Message = $"Thanh toán thành công! Đã kích hoạt gói {payment.Plan.PlanName}",
                            OrderCode = orderCode,
                            PaidAt = payment.PaidAt,
                            Amount = payment.Amount,
                            PlanName = payment.Plan.PlanName,
                            ExpiryDate = payment.User.PlanExpiryDate?.ToDateTime(TimeOnly.MinValue)
                        });
                    }
                    else if (paymentInfo.status == "CANCELLED" && payment.PaymentStatus == "Pending")
                    {
                        payment.PaymentStatus = "Failed"; // Use correct constraint value
                        _context.Payments.Update(payment);
                        await _context.SaveChangesAsync();

                        return Ok(new {
                            Success = false,
                            PaymentStatus = payment.PaymentStatus,
                            Message = "Thanh toán đã bị hủy",
                            OrderCode = orderCode
                        });
                    }
                    else if (paymentInfo.status == "PENDING")
                    {
                        return Ok(new {
                            Success = false,
                            PaymentStatus = "Pending",
                            Message = "Thanh toán đang chờ xử lý",
                            OrderCode = orderCode
                        });
                    }
                    else
                    {
                        return Ok(new {
                            Success = true,
                            PaymentStatus = payment.PaymentStatus,
                            Message = "Thanh toán đã được xử lý trước đó",
                            OrderCode = orderCode,
                            PayOSStatus = paymentInfo.status
                        });
                    }
                }
                else
                {
                    return Ok(new {
                        Success = false,
                        PaymentStatus = payment.PaymentStatus,
                        Message = "Không thể lấy thông tin thanh toán từ PayOS",
                        OrderCode = orderCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking PayOS payment status for order: {orderCode}");
                return StatusCode(500, new {
                    Success = false,
                    Message = "Có lỗi xảy ra khi kiểm tra trạng thái thanh toán PayOS",
                    OrderCode = orderCode,
                    Error = ex.Message
                });
            }
        }
    }
}
