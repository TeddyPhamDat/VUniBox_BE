namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request model for completing a payment after successful payment processing
    /// </summary>
    public class PaymentCompletionRequest
    {
        /// <summary>
        /// The payment ID that was processed
        /// </summary>
        public int PaymentId { get; set; }

        /// <summary>
        /// Payment status (SUCCESS, FAILED, etc.)
        /// </summary>
        public string PaymentStatus { get; set; } = string.Empty;

        /// <summary>
        /// External transaction ID from payment gateway
        /// </summary>
        public string? TransactionId { get; set; }
    }
}
