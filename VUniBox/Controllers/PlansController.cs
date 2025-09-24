using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Controllers
{
    /// <summary>
    /// Controller quản lý các gói dịch vụ VUniBox
    /// </summary>
    [ApiController]
    [Route("api/plan")]
    public class PlansController : ControllerBase
    {
        private readonly VUniBoxContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlansController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public PlansController(VUniBoxContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a list of all available service plans.
        /// </summary>
        /// <returns>A <see cref="ActionResult"/> with a list of <see cref="PlanDto"/> objects.</returns>
        [HttpGet]
        public async Task<ActionResult<PlansListResponse>> GetAllPlans()
        {
            try
            {
                var plans = await _context.Plans
                    .OrderBy(p => p.Price)
                    .Select(p => new PlanDto
                    {
                        PlanId = p.PlanId,
                        PlanName = p.PlanName,
                        StorageLimitMb = p.StorageLimitMb,
                        StorageDisplay = p.StorageLimitMb < 1024 
                            ? $"{p.StorageLimitMb} MB" 
                            : $"{p.StorageLimitMb / 1024} GB",
                        CitationLimit = p.CitationLimit,
                        ChatbotLimit = p.ChatbotLimit,
                        Price = p.Price,
                        PriceDisplay = p.Price == 0 
                            ? "Miễn phí" 
                            : $"{p.Price:N0} VND/tháng",
                        DurationMonths = p.DurationMonths,
                        Features = new List<string>
                        {
                            $"Dung lượng lưu trữ: {(p.StorageLimitMb < 1024 ? $"{p.StorageLimitMb}MB" : $"{p.StorageLimitMb / 1024}GB")}",
                            $"Tự động trích dẫn & phân loại tài liệu: {p.CitationLimit} lần",
                            $"Chatbox: {p.ChatbotLimit} lần"
                        }
                    })
                    .ToListAsync();

                return Ok(new PlansListResponse
                {
                    Success = true,
                    Plans = plans,
                    Message = "Lấy danh sách gói dịch vụ thành công",
                    TotalPlans = plans.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PlansListResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy danh sách gói dịch vụ",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Retrieves the detailed information for a specific service plan.
        /// </summary>
        /// <param name="planId">The ID of the service plan.</param>
        /// <returns>A <see cref="ActionResult"/> with the detailed <see cref="PlanDto"/>.</returns>
        [HttpGet("{planId}")]
        public async Task<ActionResult<PlanDetailResponse>> GetPlanById(int planId)
        {
            try
            {
                var plan = await _context.Plans
                    .Where(p => p.PlanId == planId)
                    .Select(p => new PlanDto
                    {
                        PlanId = p.PlanId,
                        PlanName = p.PlanName,
                        StorageLimitMb = p.StorageLimitMb,
                        StorageDisplay = p.StorageLimitMb < 1024 
                            ? $"{p.StorageLimitMb} MB" 
                            : $"{p.StorageLimitMb / 1024} GB",
                        CitationLimit = p.CitationLimit,
                        ChatbotLimit = p.ChatbotLimit,
                        Price = p.Price,
                        PriceDisplay = p.Price == 0 
                            ? "Miễn phí" 
                            : $"{p.Price:N0} VND/tháng",
                        DurationMonths = p.DurationMonths,
                        Features = new List<string>
                        {
                            $"Dung lượng lưu trữ: {(p.StorageLimitMb < 1024 ? $"{p.StorageLimitMb}MB" : $"{p.StorageLimitMb / 1024}GB")}",
                            $"Tự động trích dẫn & phân loại tài liệu: {p.CitationLimit} lần",
                            $"Chatbox: {p.ChatbotLimit} lần"
                        }
                    })
                    .FirstOrDefaultAsync();

                if (plan == null)
                {
                    return NotFound(new PlanDetailResponse
                    {
                        Success = false,
                        Message = "Không tìm thấy gói dịch vụ với ID này"
                    });
                }

                return Ok(new PlanDetailResponse
                {
                    Success = true,
                    Plan = plan,
                    Message = "Lấy thông tin gói dịch vụ thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PlanDetailResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy thông tin gói dịch vụ",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Compares the features of all available service plans.
        /// </summary>
        /// <returns>A <see cref="ActionResult"/> with a comparison table of plan features.</returns>
        [HttpGet("compare")]
        public async Task<ActionResult<PlanComparisonResponse>> ComparePlans()
        {
            try
            {
                var plans = await _context.Plans
                    .OrderBy(p => p.Price)
                    .ToListAsync();

                var comparison = new PlanComparisonResponse
                {
                    Success = true,
                    Message = "So sánh gói dịch vụ thành công",
                    Comparison = new PlanComparison
                    {
                        Plans = plans.Select(p => new PlanSummary
                        {
                            PlanId = p.PlanId,
                            PlanName = p.PlanName,
                            Price = p.Price,
                            PriceDisplay = p.Price == 0 ? "Miễn phí" : $"{p.Price:N0} VND/tháng"
                        }).ToList(),
                        
                        Features = new List<FeatureComparison>
                        {
                            new FeatureComparison
                            {
                                FeatureName = "Dung lượng lưu trữ",
                                Values = plans.Select(p => p.StorageLimitMb < 1024 
                                    ? $"{p.StorageLimitMb} MB" 
                                    : $"{p.StorageLimitMb / 1024} GB").ToList()
                            },
                            new FeatureComparison
                            {
                                FeatureName = "Trích dẫn & phân loại tài liệu",
                                Values = plans.Select(p => $"{p.CitationLimit} lần").ToList()
                            },
                            new FeatureComparison
                            {
                                FeatureName = "Chatbox AI",
                                Values = plans.Select(p => $"{p.ChatbotLimit} lần").ToList()
                            }
                        }
                    }
                };

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PlanComparisonResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi so sánh gói dịch vụ",
                    Error = ex.Message
                });
            }
        }
    }
}
