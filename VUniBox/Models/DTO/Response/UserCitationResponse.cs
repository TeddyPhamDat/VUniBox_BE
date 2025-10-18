namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Simplified citation response for user listing - contains only essential information
    /// </summary>
    public class UserCitationResponse
    {
        /// <summary>
        /// Document ID
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Citation style (APA, MLA, etc.)
        /// </summary>
        public string Style { get; set; } = string.Empty;

        /// <summary>
        /// Full formatted citation
        /// </summary>
        public string FormattedCitation { get; set; } = string.Empty;

        /// <summary>
        /// In-text citation format
        /// </summary>
        public string InTextCitation { get; set; } = string.Empty;

        // Basic metadata for user reference
        /// <summary>
        /// Document title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Author name
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Publication year
        /// </summary>
        public string Year { get; set; } = string.Empty;

        /// <summary>
        /// Publisher name
        /// </summary>
        public string Publisher { get; set; } = string.Empty;

        /// <summary>
        /// Creation date of the citation
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Constructor from CitationMetadataDto
        /// </summary>
        public UserCitationResponse(CitationMetadataDto metadata)
        {
            DocumentId = metadata.DocumentId;
            Title = metadata.Title;
            Author = metadata.Author;
            Year = metadata.PublicationDate?.ToString("yyyy") ?? "N/A";
            Publisher = metadata.Publisher;
            CreatedAt = metadata.CreatedAt ?? DateTime.Now;
            
            // Style, FormattedCitation, InTextCitation sẽ được set từ bên ngoài
            Style = string.Empty;
            FormattedCitation = string.Empty;
            InTextCitation = string.Empty;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public UserCitationResponse()
        {
        }
    }
}