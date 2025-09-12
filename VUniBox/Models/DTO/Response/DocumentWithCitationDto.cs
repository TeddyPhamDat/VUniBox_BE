using VUniBox.Models;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Document DTO with complete metadata and citation
    /// Used for folder view where users need citation information
    /// </summary>
    public class DocumentWithCitationDto : DocumentWithMetadataDto
    {
        /// <summary>
        /// Gets or sets the citation information for the document (single citation only).
        /// </summary>
        public CitationInfo? Citation { get; set; }
        
        /// <summary>
        /// Gets or sets the formatted citation (direct access).
        /// </summary>
        public string? FormattedCitation { get; set; }
        /// <summary>
        /// Gets or sets the in-text citation (direct access).
        /// </summary>
        public string? InTextCitation { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithCitationDto"/> class.
        /// </summary>
        public DocumentWithCitationDto() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithCitationDto"/> class with document data.
        /// </summary>
        /// <param name="document">The document entity.</param>
        public DocumentWithCitationDto(Documents document) : base(document)
        {
            // Base class handles basic metadata (FileSize, FileType)
            // Citations will be populated separately by the controller
        }

        /// <summary>
        /// Adds citation information to the document DTO (single citation only).
        /// If multiple citations exist, uses the latest one or APA if available.
        /// </summary>
        /// <param name="documentCitations">The list of document citations.</param>
        public void AddCitation(List<Citations> documentCitations)
        {
            if (!documentCitations.Any())
            {
                Citation = null;
                FormattedCitation = null;
                InTextCitation = null;
                return;
            }

            // Use the latest citation (most recently created)
            var latestCitation = documentCitations.OrderByDescending(c => c.CreatedAt).First();
            
            Citation = new CitationInfo
            {
                Style = latestCitation.Style,
                FormattedCitation = latestCitation.FormattedCitation,
                InTextCitation = latestCitation.InTextCitation,
                CreatedAt = latestCitation.CreatedAt ?? DateTime.UtcNow
            };

            // Set direct access properties
            FormattedCitation = latestCitation.FormattedCitation;
            InTextCitation = latestCitation.InTextCitation;
        }
    }

    /// <summary>
    /// Represents citation information for a specific style.
    /// </summary>
    public class CitationInfo
    {
        /// <summary>
        /// Gets or sets the style of the citation (e.g., "APA").
        /// </summary>
        public string Style { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the formatted citation text.
        /// </summary>
        public string FormattedCitation { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the in-text citation text.
        /// </summary>
        public string InTextCitation { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the creation date of the citation.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
