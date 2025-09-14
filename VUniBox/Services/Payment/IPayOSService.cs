using Net.payOS.Types;

namespace VUniBox.Services.Payment
{
    /// <summary>
    /// Interface cho PayOS payment service
    /// </summary>
    public interface IPayOSService
    {
        /// <summary>
        /// Tạo payment link với PayOS
        /// </summary>
        /// <param name="request">Thông tin payment request</param>
        /// <returns>PayOS payment response</returns>
        Task<PayOSPaymentResponse> CreatePaymentAsync(PayOSPaymentRequest request);

        /// <summary>
        /// Lấy thông tin payment từ PayOS
        /// </summary>
        /// <param name="orderCode">Mã đơn hàng</param>
        /// <returns>Thông tin payment</returns>
        Task<PaymentData?> GetPaymentInfoAsync(long orderCode);

        /// <summary>
        /// Get PaymentLinkInformation directly for status checking
        /// </summary>
        /// <param name="orderCode">Mã đơn hàng</param>
        /// <returns>PaymentLinkInformation với thông tin status</returns>
        Task<PaymentLinkInformation?> GetPaymentLinkInfoAsync(long orderCode);

        /// <summary>
        /// Hủy payment
        /// </summary>
        /// <param name="orderCode">Mã đơn hàng</param>
        /// <param name="cancellationReason">Lý do hủy</param>
        /// <returns>Kết quả hủy payment</returns>
        Task<PaymentData?> CancelPaymentAsync(long orderCode, string? cancellationReason = null);

        /// <summary>
        /// Verify webhook signature từ PayOS
        /// </summary>
        /// <param name="requestBody">Request body</param>
        /// <param name="signature">Signature từ header</param>
        /// <returns>True nếu signature hợp lệ</returns>
        bool VerifyWebhookSignature(string requestBody, string signature);
    }

    /// <summary>
    /// PayOS payment request model
    /// </summary>
    public class PayOSPaymentRequest
    {
        /// <summary>
        /// Mã đơn hàng (unique)
        /// </summary>
        public long OrderCode { get; set; }

        /// <summary>
        /// Số tiền thanh toán
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Mô tả thanh toán
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Tên người mua
        /// </summary>
        public string BuyerName { get; set; } = string.Empty;

        /// <summary>
        /// Email người mua
        /// </summary>
        public string BuyerEmail { get; set; } = string.Empty;

        /// <summary>
        /// Số điện thoại người mua
        /// </summary>
        public string BuyerPhone { get; set; } = string.Empty;

        /// <summary>
        /// Địa chỉ người mua
        /// </summary>
        public string BuyerAddress { get; set; } = string.Empty;

        /// <summary>
        /// ID người dùng trong hệ thống
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// ID gói dịch vụ
        /// </summary>
        public int PlanId { get; set; }
    }

    /// <summary>
    /// PayOS payment response model
    /// </summary>
    public class PayOSPaymentResponse
    {
        /// <summary>
        /// Trạng thái thành công
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Thông báo
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Mã đơn hàng
        /// </summary>
        public long OrderCode { get; set; }

        /// <summary>
        /// URL thanh toán
        /// </summary>
        public string? CheckoutUrl { get; set; }

        /// <summary>
        /// URL QR code
        /// </summary>
        public string? QrCode { get; set; }

        /// <summary>
        /// Số tiền
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Mô tả
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// ID payment trong database
        /// </summary>
        public int? PaymentId { get; set; }

        /// <summary>
        /// Thông tin lỗi nếu có
        /// </summary>
        public string? Error { get; set; }
    }
}