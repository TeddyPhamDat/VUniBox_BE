using VUniBox.Models;

namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Document DTO with citation information included
    /// Used for APIs that need to return documents with their citations
    /// </summary>
    public class DocumentWithCitationsDto : DocumentDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithCitationsDto"/> class.
        /// </summary>
        public DocumentWithCitationsDto() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentWithCitationsDto"/> class with document data.
        /// </summary>
        /// <param name="document">The document entity.</param>
        public DocumentWithCitationsDto(Documents document) : base(document)
        {
            // Base constructor handles all document properties
            // Citations will be populated separately by the controller
        }

        /// <summary>
        /// Initializes a new instance with document and citation data.
        /// </summary>
        /// <param name="document">The document entity.</param>
        /// <param name="citation">The citation entity.</param>
        public DocumentWithCitationsDto(Documents document, Citations? citation) : base(document)
        {
            if (citation != null)
            {
                CitationStyle = citation.Style ?? "APA";
                FormattedCitation = citation.FormattedCitation ?? string.Empty;
                InTextCitation = citation.InTextCitation ?? string.Empty;
            }
        }
    }
}