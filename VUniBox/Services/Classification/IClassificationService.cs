using VUniBox.Models.DTO.Request;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Classification
{
    public interface IClassificationService
    {
        Task<ClassificationResponse> ClassifyAsync(ClassificationRequest request);
    }
}


