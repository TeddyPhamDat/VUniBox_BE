namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to regenerate citations for a document with a new citation style.
    /// </summary>
    public class CitationRegenerateRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the document for which to regenerate citations.
        /// </summary>
        public int DocumentId { get; set; }
        /// <summary>
        /// Gets or sets the new desired citation style (e.g., "APA", "MLA").
        /// </summary>
        public string NewCitationStyle { get; set; }
    }
}
