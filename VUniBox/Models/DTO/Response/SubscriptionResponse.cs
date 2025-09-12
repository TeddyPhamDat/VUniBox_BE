namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response cho thông tin đăng ký hiện tại
    /// </summary>
    public class CurrentSubscriptionResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the current plan details.
        /// </summary>
        public PlanDto? CurrentPlan { get; set; }
        /// <summary>
        /// Gets or sets the expiry date of the current plan.
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the current plan is active.
        /// </summary>
        public bool IsActive { get; set; }
        /// <summary>
        /// Gets or sets the number of days remaining until the plan expires.
        /// </summary>
        public int? DaysRemaining { get; set; }
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents the response for a request to retrieve subscription history.
    /// </summary>
    public class SubscriptionHistoryResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the list of subscription history items.
        /// </summary>
        public List<SubscriptionHistoryItem> Subscriptions { get; set; } = new List<SubscriptionHistoryItem>();
        /// <summary>
        /// Gets or sets the total number of subscriptions in the history.
        /// </summary>
        public int TotalSubscriptions { get; set; }
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents a single item in the subscription history.
    /// </summary>
    public class SubscriptionHistoryItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the subscription.
        /// </summary>
        public int SubscriptionId { get; set; }
        /// <summary>
        /// Gets or sets the name of the plan associated with this subscription.
        /// </summary>
        public string PlanName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the start date of the subscription.
        /// </summary>
        public DateTime StartDate { get; set; }
        /// <summary>
        /// Gets or sets the end date of the subscription.
        /// </summary>
        public DateTime? EndDate { get; set; }
        /// <summary>
        /// Gets or sets the status of the subscription (e.g., "ACTIVE", "EXPIRED").
        /// </summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the amount paid for the subscription.
        /// </summary>
        public decimal Amount { get; set; }
        /// <summary>
        /// Gets or sets the status of the payment for this subscription.
        /// </summary>
        public string PaymentStatus { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the payment was made.
        /// </summary>
        public DateTime? PaidAt { get; set; }
        /// <summary>
        /// Gets a display-friendly string for the subscription status.
        /// </summary>
        public string StatusDisplay => Status switch
        {
            "ACTIVE" => "Đang hoạt động",
            "EXPIRED" => "Đã hết hạn",
            "PENDING" => "Chờ thanh toán",
            "CANCELLED" => "Đã hủy",
            _ => Status
        };
        /// <summary>
        /// Gets a display-friendly string for the amount paid.
        /// </summary>
        public string AmountDisplay => Amount == 0 ? "Miễn phí" : $"{Amount:N0} VND";
    }
}
