using System.Collections.Generic;
using System.Threading.Tasks;
using VUniBox.Models.DTO.Response;
using VUniBox.Models;

namespace VUniBox.Services.Citation
{
    public interface ICitationManagementService
    {
        Task<CitationResponse> GenerateCitationAsync(int documentId, string citationStyle);
        Task<CitationResponse> RegenerateCitationAsync(int documentId, string newCitationStyle);
        Task<(string FormattedCitation, string InTextCitation)> GenerateQuickCitationAsync(Documents document, string citationStyle = "APA");
        Task<(string FormattedCitation, string InTextCitation)> GenerateEnhancedCitationAsync(Documents document, string citationStyle = "APA");
        
        // Original methods returning full metadata
        Task<IEnumerable<CitationMetadataDto>> GetUserCitationsAsync(int userId);
        Task<IEnumerable<CitationMetadataDto>> GetUserCitationsSortedByAuthorAsync(int userId);
        
        // Simplified methods returning only essential information for user listing
        Task<IEnumerable<UserCitationResponse>> GetUserCitationsSimplifiedAsync(int userId);
        Task<IEnumerable<UserCitationResponse>> GetUserCitationsSimplifiedSortedByAuthorAsync(int userId);
    }
}
