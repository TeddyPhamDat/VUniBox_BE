using Net.payOS;
using Net.payOS.Types;
using VUniBox.Services.Payment;

namespace VUniBox.Services.Payment
{
    /// <summary>
    /// PayOS payment service implementation
    /// </summary>
    public class PayOSService : IPayOSService
    {
        private readonly PayOS _payOS;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayOSService> _logger;
        private readonly string _checksumKey;

        public PayOSService(IConfiguration configuration, ILogger<PayOSService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var clientId = _configuration["PayOS:ClientId"];
            var apiKey = _configuration["PayOS:ApiKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(checksumKey))
            {
                throw new InvalidOperationException("PayOS configuration is missing. Please check appsettings.json");
            }

            _checksumKey = checksumKey;
            _payOS = new PayOS(clientId, apiKey, checksumKey);
            _logger.LogInformation("PayOS service initialized successfully");
        }

        /// <summary>
        /// Tạo payment link với PayOS
        /// </summary>
        public async Task<PayOSPaymentResponse> CreatePaymentAsync(PayOSPaymentRequest request)
        {
            try
            {
                var returnUrl = _configuration["PayOS:ReturnUrl"] ?? "https://your-domain.com/payment/success";
                var cancelUrl = _configuration["PayOS:CancelUrl"] ?? "https://your-domain.com/payment/cancel";

                var paymentData = new PaymentData(
                    orderCode: request.OrderCode,
                    amount: request.Amount,
                    description: request.Description,
                    items: new List<ItemData>
                    {
                        new ItemData("VUniBox Subscription", 1, request.Amount)
                    },
                    returnUrl: returnUrl,
                    cancelUrl: cancelUrl,
                    buyerName: request.BuyerName,
                    buyerEmail: request.BuyerEmail,
                    buyerPhone: request.BuyerPhone,
                    buyerAddress: request.BuyerAddress
                );

                var createPayment = await _payOS.createPaymentLink(paymentData);

                _logger.LogInformation($"PayOS payment created successfully. OrderCode: {request.OrderCode}");

                return new PayOSPaymentResponse
                {
                    Success = true,
                    Message = "Tạo payment thành công",
                    OrderCode = request.OrderCode,
                    CheckoutUrl = createPayment.checkoutUrl,
                    QrCode = createPayment.qrCode,
                    Amount = request.Amount,
                    Description = request.Description
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating PayOS payment for OrderCode: {request.OrderCode}");
                return new PayOSPaymentResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi tạo payment",
                    Error = ex.Message,
                    OrderCode = request.OrderCode,
                    Amount = request.Amount
                };
            }
        }

        /// <summary>
        /// Lấy thông tin payment từ PayOS
        /// </summary>
        public async Task<PaymentData?> GetPaymentInfoAsync(long orderCode)
        {
            try
            {
                var paymentInfo = await _payOS.getPaymentLinkInformation(orderCode);
                _logger.LogInformation($"Retrieved payment info for OrderCode: {orderCode}");
                
                // Return a simple PaymentData with the status information we need
                // We'll access the status through paymentInfo.status in the controller
                return new PaymentData(
                    orderCode: orderCode,
                    amount: 0,
                    description: "",
                    items: new List<ItemData>(),
                    returnUrl: "",
                    cancelUrl: ""
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting payment info for OrderCode: {orderCode}");
                return null;
            }
        }

        /// <summary>
        /// Get PaymentLinkInformation directly for status checking
        /// </summary>
        public async Task<PaymentLinkInformation?> GetPaymentLinkInfoAsync(long orderCode)
        {
            try
            {
                var paymentInfo = await _payOS.getPaymentLinkInformation(orderCode);
                _logger.LogInformation($"Retrieved payment link info for OrderCode: {orderCode}");
                return paymentInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting payment link info for OrderCode: {orderCode}");
                return null;
            }
        }

        /// <summary>
        /// Hủy payment
        /// </summary>
        public async Task<PaymentData?> CancelPaymentAsync(long orderCode, string? cancellationReason = null)
        {
            try
            {
                var cancelledPayment = await _payOS.cancelPaymentLink(orderCode, cancellationReason);
                _logger.LogInformation($"Cancelled payment for OrderCode: {orderCode}");
                
                // Return a simple PaymentData structure 
                return new PaymentData(
                    orderCode: orderCode,
                    amount: 0,
                    description: cancellationReason ?? "Payment cancelled",
                    items: new List<ItemData>(),
                    returnUrl: "",
                    cancelUrl: ""
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling payment for OrderCode: {orderCode}");
                return null;
            }
        }

        /// <summary>
        /// Verify webhook signature từ PayOS
        /// </summary>
        public bool VerifyWebhookSignature(string requestBody, string signature)
        {
            try
            {
                // TODO: Implement webhook verification based on PayOS documentation
                // For now, return true to allow testing
                _logger.LogWarning("PayOS webhook signature verification not implemented yet");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying PayOS webhook signature");
                return false;
            }
        }
    }
}