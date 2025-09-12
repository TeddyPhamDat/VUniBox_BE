namespace VUniBox.Models.DTO.Request
{
    /// <summary>
    /// Represents a request to generate citations for a document in a specific style.
    /// </summary>
    public class CitationGenerateRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the document for which to generate citations.
        /// </summary>
        public int DocumentId { get; set; }
        /// <summary>
        /// Gets or sets the desired citation style (e.g., "APA", "MLA").
        /// </summary>
        public string CitationStyle { get; set; }
    }
}
