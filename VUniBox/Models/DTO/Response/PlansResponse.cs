namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response cho danh sách tất cả gói dịch vụ
    /// </summary>
    public class PlansListResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the list of plans.
        /// </summary>
        public List<PlanDto> Plans { get; set; } = new List<PlanDto>();
        /// <summary>
        /// Gets or sets a message related to the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the total number of plans.
        /// </summary>
        public int TotalPlans { get; set; }
    }

    /// <summary>
    /// Response cho chi tiết một gói dịch vụ
    /// </summary>
    public class PlanDetailResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the detailed plan information.
        /// </summary>
        public PlanDto? Plan { get; set; }
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
    /// Response cho so sánh các gói dịch vụ
    /// </summary>
    public class PlanComparisonResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the plan comparison details.
        /// </summary>
        public PlanComparison? Comparison { get; set; }
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
    /// DTO cho thông tin gói dịch vụ
    /// </summary>
    public class PlanDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the plan.
        /// </summary>
        public int PlanId { get; set; }
        /// <summary>
        /// Gets or sets the name of the plan.
        /// </summary>
        public string PlanName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the storage limit in Megabytes for the plan.
        /// </summary>
        public int StorageLimitMb { get; set; }
        /// <summary>
        /// Gets or sets the display string for storage limit (e.g., "100 MB", "1 GB").
        /// </summary>
        public string StorageDisplay { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the citation generation limit for the plan.
        /// </summary>
        public int CitationLimit { get; set; }
        /// <summary>
        /// Gets or sets the chatbot usage limit for the plan.
        /// </summary>
        public int ChatbotLimit { get; set; }
        /// <summary>
        /// Gets or sets the price of the plan.
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// Gets or sets the display string for the plan price (e.g., "Miễn phí", "100.000 VND/tháng").
        /// </summary>
        public string PriceDisplay { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the duration of the plan in months.
        /// </summary>
        public int? DurationMonths { get; set; }
        /// <summary>
        /// Gets or sets a list of features included in the plan.
        /// </summary>
        public List<string> Features { get; set; } = new List<string>();
        /// <summary>
        /// Gets or sets a value indicating whether the plan is popular.
        /// </summary>
        public bool IsPopular { get; set; } = false;
        /// <summary>
        /// Gets or sets a value indicating whether the plan is recommended.
        /// </summary>
        public bool IsRecommended { get; set; } = false;
    }

    /// <summary>
    /// DTO for plan comparison details.
    /// </summary>
    public class PlanComparison
    {
        /// <summary>
        /// Gets or sets the list of summarized plans for comparison.
        /// </summary>
        public List<PlanSummary> Plans { get; set; } = new List<PlanSummary>();
        /// <summary>
        /// Gets or sets the list of feature comparisons across different plans.
        /// </summary>
        public List<FeatureComparison> Features { get; set; } = new List<FeatureComparison>();
    }

    /// <summary>
    /// DTO for a summarized plan in the comparison table.
    /// </summary>
    public class PlanSummary
    {
        /// <summary>
        /// Gets or sets the unique identifier for the plan.
        /// </summary>
        public int PlanId { get; set; }
        /// <summary>
        /// Gets or sets the name of the plan.
        /// </summary>
        public string PlanName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the price of the plan.
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// Gets or sets the display string for the plan price.
        /// </summary>
        public string PriceDisplay { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for comparing a single feature across multiple plans.
    /// </summary>
    public class FeatureComparison
    {
        /// <summary>
        /// Gets or sets the name of the feature (e.g., "Dung lượng lưu trữ").
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the list of values for this feature, corresponding to each plan.
        /// </summary>
        public List<string> Values { get; set; } = new List<string>();
    }

    /// <summary>
    /// Response cho đăng ký gói dịch vụ
    /// </summary>
    public class PlanSubscriptionResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the subscription operation was successful.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier for the created subscription.
        /// </summary>
        public int? SubscriptionId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier for the created payment record.
        /// </summary>
        public int? PaymentId { get; set; }
        /// <summary>
        /// Gets or sets the URL for completing the payment.
        /// </summary>
        public string PaymentUrl { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the QR code URL for payment, if applicable.
        /// </summary>
        public string QrCodeUrl { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the amount to be paid for the subscription.
        /// </summary>
        public decimal Amount { get; set; }
        /// <summary>
        /// Gets or sets the expiry date of the subscription.
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
        /// <summary>
        /// Gets or sets a message related to the subscription operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets an error message if the subscription operation failed.
        /// </summary>
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the plan name.
        /// </summary>
        public string? PlanName { get; set; }
        /// <summary>
        /// Gets or sets the subscription start date.
        /// </summary>
        public string? StartDate { get; set; }
        /// <summary>
        /// Gets or sets the subscription end date.
        /// </summary>
        public string? EndDate { get; set; }
        /// <summary>
        /// Gets or sets the subscription status.
        /// </summary>
        public string? Status { get; set; }
        /// <summary>
        /// Gets or sets whether payment is required.
        /// </summary>
        public bool RequirePayment { get; set; }
        /// <summary>
        /// Gets or sets the PayOS transaction ID.
        /// </summary>
        public string? PayOsTransactionId { get; set; }
        /// <summary>
        /// Gets or sets the PayOS order code.
        /// </summary>
        public long? OrderCode { get; set; }
    }
}
