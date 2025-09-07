using System.Threading.Tasks;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Classification
{
    public interface IGeminiClassificationService
    {
        Task<ClassificationResponse> ClassifyUrlWithAIAsync(string url);
    }
}
