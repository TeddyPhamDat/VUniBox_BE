namespace VUniBox.Models.DTO.Response
{
    /// <summary>
    /// Response for final action confirmation after metadata extraction
    /// Contains complete metadata for citation generation
    /// </summary>
    public class ActionConfirmationResponse
    {
        public bool Success { get; set; }
        public int DocumentId { get; set; }
        public string Action { get; set; } = string.Empty; // "saved" or "moved_to_trash"
        public string Message { get; set; } = string.Empty;
        public bool IsInTrash { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Location { get; set; } = string.Empty;
        
        // Complete metadata for citation generation
        public CitationMetadataDto Metadata { get; set; } = new();
    }
}