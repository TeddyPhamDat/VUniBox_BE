using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;

namespace VUniBox.Services.Citation
{
    public class CitationManagementService : ICitationManagementService
    {
        private readonly VUniBoxContext _context;
        private readonly IGeminiCitationService _geminiCitationService;

        public CitationManagementService(VUniBoxContext context, IGeminiCitationService geminiCitationService)
        {
            _context = context;
            _geminiCitationService = geminiCitationService;
        }

        public async Task<CitationResponse> GenerateCitationAsync(int documentId, string citationStyle)
        {
            try
            {
                var document = await _context.Documents.FindAsync(documentId);
                if (document == null)
                {
                    Console.WriteLine($"[CitationManagementService] Document with ID {documentId} not found");
                    return null;
                }

                Console.WriteLine($"[CitationManagementService] Found document: ID={document.DocumentId}, Title={document.Title}");

                // Check if citation already exists for this document and style
                var existingCitation = await _context.Citations
                    .FirstOrDefaultAsync(c => c.DocumentId == documentId && c.Style == citationStyle);

                if (existingCitation != null)
                {
                    Console.WriteLine($"[CitationManagementService] Returning existing citation for document {documentId} in {citationStyle} style");
                    return new CitationResponse
                    {
                        DocumentId = documentId,
                        Style = citationStyle,
                        FormattedCitation = existingCitation.FormattedCitation,
                        InTextCitation = existingCitation.InTextCitation
                    };
                }

                // Extract and validate metadata with detailed logging
                var citationData = ExtractCitationData(document);
                Console.WriteLine($"[CitationManagementService] Extracted citation data: {citationData}");

                // Generate citation using deterministic service
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    citationData.Title,
                    citationData.Authors,
                    citationData.Year,
                    citationData.PublicationDate,
                    citationData.Type,
                    citationData.Url,
                    citationStyle);

                Console.WriteLine($"[CitationManagementService] Generated citations - Formatted: {formatted}, InText: {inText}");

                // Validate generated citations
                if (string.IsNullOrWhiteSpace(formatted) || string.IsNullOrWhiteSpace(inText))
                {
                    throw new Exception("Generated citations are empty or invalid");
                }

                // Save citation to database
                var citation = new Citations
                {
                    DocumentId = documentId,
                    UserId = document.UserId,
                    Style = citationStyle,
                    FormattedCitation = formatted,
                    InTextCitation = inText,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Citations.Add(citation);
                
                // Update document with citation style
                document.CitationStyle = citationStyle;
                
                await _context.SaveChangesAsync();

                Console.WriteLine($"[CitationManagementService] Successfully generated and saved citation for document {documentId}");

                return new CitationResponse
                {
                    DocumentId = documentId,
                    Style = citationStyle,
                    FormattedCitation = formatted,
                    InTextCitation = inText
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error generating citation for document {documentId}: {ex.Message}");
                throw new Exception($"Failed to generate citation: {ex.Message}", ex);
            }
        }

        public async Task<CitationResponse> RegenerateCitationAsync(int documentId, string newCitationStyle)
        {
            try
            {
                var document = await _context.Documents.FindAsync(documentId);
                if (document == null)
                {
                    Console.WriteLine($"[CitationManagementService] Document with ID {documentId} not found for regeneration");
                    return null;
                }

                // Remove existing citations for this document
                var existingCitations = await _context.Citations
                    .Where(c => c.DocumentId == documentId)
                    .ToListAsync();

                if (existingCitations.Any())
                {
                    Console.WriteLine($"[CitationManagementService] Removing {existingCitations.Count} existing citations for document {documentId}");
                    _context.Citations.RemoveRange(existingCitations);
                }

                // Extract and validate metadata
                var citationData = ExtractCitationData(document);
                Console.WriteLine($"[CitationManagementService] Extracted citation data for regeneration: {citationData}");

                // Generate new citation
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    citationData.Title,
                    citationData.Authors,
                    citationData.Year,
                    citationData.PublicationDate,
                    citationData.Type,
                    citationData.Url,
                    newCitationStyle);

                // Validate generated citations
                if (string.IsNullOrWhiteSpace(formatted) || string.IsNullOrWhiteSpace(inText))
                {
                    throw new Exception("Regenerated citations are empty or invalid");
                }

                // Save new citation
                var newCitation = new Citations
                {
                    DocumentId = documentId,
                    UserId = document.UserId,
                    Style = newCitationStyle,
                    FormattedCitation = formatted,
                    InTextCitation = inText,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Citations.Add(newCitation);
                
                // Update document with new citation style
                document.CitationStyle = newCitationStyle;
                
                await _context.SaveChangesAsync();

                Console.WriteLine($"[CitationManagementService] Successfully regenerated citation for document {documentId} in {newCitationStyle} style");

                return new CitationResponse
                {
                    DocumentId = documentId,
                    Style = newCitationStyle,
                    FormattedCitation = formatted,
                    InTextCitation = inText
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error regenerating citation for document {documentId}: {ex.Message}");
                throw new Exception($"Failed to regenerate citation: {ex.Message}", ex);
            }
        }

        private CitationDataExtract ExtractCitationData(Documents document)
        {
            // Detailed logging of raw document data
            Console.WriteLine($"[CitationManagementService] Raw document data:");
            Console.WriteLine($"  - Title: '{document.Title}'");
            Console.WriteLine($"  - Author: '{document.Author}'");
            Console.WriteLine($"  - Authors: '{document.Authors}'");
            Console.WriteLine($"  - Year: {document.Year}");
            Console.WriteLine($"  - PublicationDate: {document.PublicationDate}");
            Console.WriteLine($"  - CreatedAt: {document.CreatedAt}");
            Console.WriteLine($"  - DocumentType: {document.DocumentType}");
            Console.WriteLine($"  - SourceUrl: '{document.SourceUrl}'");

            // Validate and extract metadata with proper fallbacks
            var title = !string.IsNullOrWhiteSpace(document.Title) ? document.Title : "Untitled Document";
            var authors = !string.IsNullOrWhiteSpace(document.Authors) ? document.Authors : 
                         (!string.IsNullOrWhiteSpace(document.Author) ? document.Author : "Unknown Author");
            
            // --- ENHANCED DATE EXTRACTION ---
            int? year = document.Year;
            string publicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "";

            // If year is still not found, try to get it from PublicationDate
            if (!year.HasValue && document.PublicationDate.HasValue)
            {
                year = document.PublicationDate.Value.Year;
                publicationDate = document.PublicationDate.Value.ToString("yyyy-MM-dd");
                Console.WriteLine($"[CitationManagementService] Extracted year from PublicationDate. Year: {year}, Date: {publicationDate}");
            }
            
            // As a fallback, use CreatedAt if no other date information is available
            if (!year.HasValue && document.CreatedAt.HasValue)
            {
                year = document.CreatedAt.Value.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = document.CreatedAt.Value.ToString("yyyy-MM-dd");
                }
                Console.WriteLine($"[CitationManagementService] Using CreatedAt as fallback date. Year: {year}, Date: {publicationDate}");
            }

            // Final fallback - use current year to avoid "n.d." format
            if (!year.HasValue)
            {
                year = DateTime.UtcNow.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }
                Console.WriteLine($"[CitationManagementService] Using current year as final fallback. Year: {year}, Date: {publicationDate}");
            }
            // --- END ENHANCED DATE EXTRACTION ---

            var type = GetDocumentTypeName(document.DocumentType);
            var url = !string.IsNullOrWhiteSpace(document.SourceUrl) ? document.SourceUrl : "";

            Console.WriteLine($"[CitationManagementService] Processed data:");
            Console.WriteLine($"  - Title: '{title}'");
            Console.WriteLine($"  - Authors: '{authors}'");
            Console.WriteLine($"  - Final Year: {year}");
            Console.WriteLine($"  - Final PublicationDate: '{publicationDate}'");
            Console.WriteLine($"  - Type: '{type}'");
            Console.WriteLine($"  - URL: '{url}'");

            return new CitationDataExtract
            {
                Title = title,
                Authors = authors,
                Year = year,
                PublicationDate = publicationDate,
                Type = type,
                Url = url
            };
        }

        private string GetDocumentTypeName(int documentType)
        {
            return documentType switch
            {
                1 => "Word Document",
                2 => "PDF Document", 
                3 => "Book",
                4 => "Research Article",
                5 => "Newspaper Article",
                6 => "Website",
                _ => "Document"
            };
        }

        // Helper class for extracting citation data
        private class CitationDataExtract
        {
            public string Title { get; set; }
            public string Authors { get; set; }
            public int? Year { get; set; }
            public string PublicationDate { get; set; }
            public string Type { get; set; }
            public string Url { get; set; }

            public override string ToString()
            {
                return $"Title: '{Title}', Authors: '{Authors}', Year: {Year}, PublicationDate: '{PublicationDate}', Type: '{Type}', URL: '{Url}'";
            }
        }
    }
}
