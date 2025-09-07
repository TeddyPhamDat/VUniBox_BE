using System.Threading.Tasks;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Citation
{
    public interface ICitationManagementService
    {
        Task<CitationResponse> GenerateCitationAsync(int documentId, string citationStyle);
        Task<CitationResponse> RegenerateCitationAsync(int documentId, string newCitationStyle);
    }
}
