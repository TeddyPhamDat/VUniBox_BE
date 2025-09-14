using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Services.Payment;
using VUniBox.Services.Subscription;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller xử lý PayOS webhook
    /// </summary>
    [ApiController]
    [Route("api/payos")]
    public class PayOSWebhookController : ControllerBase
    {
        private readonly VUniBoxContext _context;
        private readonly IPayOSService _payOSService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<PayOSWebhookController> _logger;

        public PayOSWebhookController(
            VUniBoxContext context,
            IPayOSService payOSService,
            ISubscriptionService subscriptionService,
            ILogger<PayOSWebhookController> logger)
        {
            _context = context;
            _payOSService = payOSService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Xử lý webhook từ PayOS khi thanh toán thành công
        /// </summary>
        /// <param name="request">Webhook data từ PayOS</param>
        /// <returns>Kết quả xử lý webhook</returns>
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook([FromBody] PayOSWebhookRequest request)
        {
            try
            {
                // Lấy signature từ header
                var signature = Request.Headers["PayOS-Signature"].FirstOrDefault();
                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogWarning("PayOS webhook received without signature");
                    return BadRequest("Missing signature");
                }

                // Verify signature
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                if (!_payOSService.VerifyWebhookSignature(requestBody, signature))
                {
                    _logger.LogWarning("Invalid PayOS webhook signature");
                    return BadRequest("Invalid signature");
                }

                _logger.LogInformation($"PayOS webhook received: OrderCode={request.OrderCode}, Status={request.Data.Status}");

                // Xử lý webhook dựa trên status
                switch (request.Data.Status?.ToUpper())
                {
                    case "PAID":
                        await HandleSuccessfulPayment(request.Data);
                        break;
                    case "CANCELLED":
                        await HandleCancelledPayment(request.Data);
                        break;
                    default:
                        _logger.LogInformation($"PayOS webhook status not handled: {request.Data.Status}");
                        break;
                }

                return Ok(new { success = true, message = "Webhook processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayOS webhook");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Xử lý khi thanh toán thành công
        /// </summary>
        private async Task HandleSuccessfulPayment(PayOSWebhookData data)
        {
            // Tìm payment record trong database
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.PayOsTransactionId == data.OrderCode.ToString() && p.PaymentStatus == "PENDING");

            if (payment == null)
            {
                _logger.LogWarning($"Payment not found for PayOS OrderCode: {data.OrderCode}");
                return;
            }

            // Cập nhật payment status
            payment.PaymentStatus = "SUCCESS";
            payment.PaidAt = DateTime.UtcNow;
            _context.Payments.Update(payment);

            // Tạo subscription cho user
            await _subscriptionService.SubscribeUserAsync(payment.UserId, payment.PlanId, payment.PaymentId);

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Payment processed successfully: PaymentId={payment.PaymentId}, UserId={payment.UserId}");
        }

        /// <summary>
        /// Xử lý khi thanh toán bị hủy
        /// </summary>
        private async Task HandleCancelledPayment(PayOSWebhookData data)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.PayOsTransactionId == data.OrderCode.ToString());

            if (payment != null)
            {
                payment.PaymentStatus = "CANCELLED";
                _context.Payments.Update(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Payment cancelled: PaymentId={payment.PaymentId}, OrderCode={data.OrderCode}");
            }
        }
    }

    /// <summary>
    /// PayOS webhook request model
    /// </summary>
    public class PayOSWebhookRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public bool Success { get; set; }
        public PayOSWebhookData Data { get; set; } = new();
        public string Signature { get; set; } = string.Empty;
        public long OrderCode { get; set; }
    }

    /// <summary>
    /// PayOS webhook data model
    /// </summary>
    public class PayOSWebhookData
    {
        public long OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string TransactionDateTime { get; set; } = string.Empty;
        public string Currency { get; set; } = "VND";
        public string PaymentLinkId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string CounterAccountBankId { get; set; } = string.Empty;
        public string CounterAccountBankName { get; set; } = string.Empty;
        public string CounterAccountName { get; set; } = string.Empty;
        public string CounterAccountNumber { get; set; } = string.Empty;
        public string VirtualAccountName { get; set; } = string.Empty;
        public string VirtualAccountNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}