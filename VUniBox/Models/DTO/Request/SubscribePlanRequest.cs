namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Request để đăng ký gói dịch vụ
    /// </summary>
    public class SubscribePlanRequest
    {
        /// <summary>
        /// Gets or sets the ID of the user subscribing to the plan.
        /// </summary>
        public int UserId { get; set; }
        /// <summary>
        /// Gets or sets the ID of the plan to subscribe to.
        /// </summary>
        public int PlanId { get; set; }
        /// <summary>
        /// Gets or sets an optional promo code to apply to the subscription.
        /// </summary>
        public string? PromoCode { get; set; }
    }
}
