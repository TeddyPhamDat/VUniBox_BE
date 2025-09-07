namespace VUniBox.Models.DTO.Response
{
    public class CitationResponse
    {
        public int DocumentId { get; set; }
        public string Style { get; set; }
        public string FormattedCitation { get; set; }
        public string InTextCitation { get; set; }
    }
}
