using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using VUniBox.DBContext;
using VUniBox.Models;
using VUniBox.Models.DTO;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.Enum;
using VUniBox.Services.Metadata;

namespace VUniBox.Services.Citation
{
    public class CitationManagementService : ICitationManagementService
    {
        private readonly VUniBoxContext _context;
        private readonly IGeminiCitationService _geminiCitationService;
        private readonly IUrlMetadataExtractor _urlMetadataExtractor;

        public CitationManagementService(VUniBoxContext context, IGeminiCitationService geminiCitationService, IUrlMetadataExtractor urlMetadataExtractor)
        {
            _context = context;
            _geminiCitationService = geminiCitationService;
            _urlMetadataExtractor = urlMetadataExtractor;
        }

        /// <summary>
        /// Generate citation quickly for API responses without URL metadata extraction
        /// Used by document list APIs for fast response times
        /// </summary>
        public async Task<(string FormattedCitation, string InTextCitation)> GenerateQuickCitationAsync(Documents document, string citationStyle = "APA")
        {
            try
            {
                // Use existing document data directly - SUPER FAST, NO EXTRACTION
                var title = document.Title ?? "Unknown Title";
                var authors = document.Authors ?? document.Author ?? "Unknown Author";
                var year = document.Year;
                var pubDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "";
                var docType = document.DocumentType.ToString();
                var url = document.SourceUrl ?? "";
                
                // Generate citation using deterministic service
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    title,
                    authors,
                    year,
                    pubDate,
                    docType,
                    url,
                    citationStyle);

                return (formatted ?? "Citation not available", inText ?? "(Citation not available)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error in quick citation generation: {ex.Message}");
                return ("Citation not available", "(Citation not available)");
            }
        }

        /// <summary>
        /// Generate enhanced citation with AI-powered metadata extraction for Author, Year, Title, Publisher
        /// Used when full citation quality is needed with complete metadata extraction
        /// </summary>
        public async Task<(string FormattedCitation, string InTextCitation)> GenerateEnhancedCitationAsync(Documents document, string citationStyle = "APA")
        {
            try
            {
                // Build comprehensive prompt for AI to extract all 4 components
                var aiPrompt = BuildComprehensiveMetadataPrompt(document.Title, document.SourceUrl, document.Authors, document.Author);
                
                // Use AI to extract Author, Year, Title, Publisher
                var (aiAuthor, aiYear, aiPublisher) = await _geminiCitationService.ExtractCitationMetadataWithAIAsync(aiPrompt);

                // Use extracted data or fallback to existing data
                var finalTitle = !string.IsNullOrWhiteSpace(document.Title) ? document.Title : "Untitled Document";
                var finalAuthor = !string.IsNullOrWhiteSpace(aiAuthor) ? aiAuthor : 
                                (!string.IsNullOrWhiteSpace(document.Authors) ? document.Authors : 
                                (!string.IsNullOrWhiteSpace(document.Author) ? document.Author : "Unknown Author"));
                var finalYear = aiYear ?? document.Year ?? DateTime.UtcNow.Year;
                var finalPublisher = !string.IsNullOrWhiteSpace(aiPublisher) ? aiPublisher : 
                                   (!string.IsNullOrWhiteSpace(document.Publisher) ? document.Publisher : "Unknown Publisher");

                // Generate citation with enhanced metadata
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    finalTitle,
                    finalAuthor,
                    finalYear,
                    document.PublicationDate?.ToString("yyyy-MM-dd") ?? "",
                    GetDocumentTypeName(document.DocumentType),
                    document.SourceUrl ?? "",
                    citationStyle);

                Console.WriteLine($"[CitationManagementService] Enhanced citation generated - Author: {finalAuthor}, Year: {finalYear}, Publisher: {finalPublisher}");
                return (formatted ?? "Citation not available", inText ?? "(Citation not available)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error in enhanced citation generation: {ex.Message}");
                return ("Citation not available", "(Citation not available)");
            }
        }

        /// <summary>
        /// Build comprehensive prompt for AI to extract Author, Year, Title, Publisher
        /// </summary>
        private string BuildComprehensiveMetadataPrompt(string title, string sourceUrl, string authors, string author)
        {
            var domain = "";
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var uri = new Uri(sourceUrl);
                    domain = uri.Host.ToLower();
                }
                catch { domain = sourceUrl; }
            }

            var existingAuthor = !string.IsNullOrWhiteSpace(authors) ? authors : author ?? "";

            return $@"Extract complete citation metadata from this academic document. You need to provide all 4 essential components for proper academic citation:

**Document Information:**
Title: ""{title}""
Source URL: {sourceUrl}
Domain: {domain}
Existing Author Data: {existingAuthor}

**Required Output (JSON format):**
Please analyze and extract:

1. **Author** - Extract or infer author name(s):
   - Look for patterns in title like ""by John Smith"", ""Smith et al."", ""John Doe and Jane Smith""
   - Use existing author data if available and reliable
   - For academic sources (ResearchGate, ArXiv, .edu): suggest ""[Platform] Researcher"" or ""Academic Research Team""
   - For commercial sources: suggest appropriate professional names

2. **Year** - Publication or access year:
   - Extract from title if mentioned (""Study 2023"", ""2024 Analysis"")
   - If not found, use current year: {DateTime.UtcNow.Year}

3. **Publisher** - Publishing entity:
   - For ResearchGate: ""ResearchGate""
   - For ScienceDirect: ""Elsevier""
   - For ArXiv: ""ArXiv""
   - For .edu domains: ""Academic Institution"" or university name if identifiable
   - For other domains: extract from domain name or use ""Online Publisher""

**Response Format:**
{{
  ""author"": ""Properly formatted author name(s)"",
  ""year"": {DateTime.UtcNow.Year},
  ""publisher"": ""Publisher name""
}}

**Examples:**
- ResearchGate paper: {{""author"": ""ResearchGate Researcher"", ""year"": 2024, ""publisher"": ""ResearchGate""}}
- Academic paper with clear author: {{""author"": ""Smith, J. et al."", ""year"": 2023, ""publisher"": ""Academic Press""}}
- ScienceDirect article: {{""author"": ""Research Team"", ""year"": 2024, ""publisher"": ""Elsevier""}}

Provide professional, citation-ready metadata that follows academic standards.";
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

                // Check if citation already exists for this document
                var existingCitation = await _context.Citations
                    .FirstOrDefaultAsync(c => c.DocumentId == documentId);

                if (existingCitation != null)
                {
                    // Update existing citation with new style (for user-selected Generate endpoint)
                    Console.WriteLine($"[CitationManagementService] Updating existing citation for document {documentId} from {existingCitation.Style} to {citationStyle} style");
                    
                    // Use AI to enhance metadata if needed (SMART & FAST)
                    var updateEnhancedData = await EnhanceDocumentMetadataWithAI(document);
                    
                    Console.WriteLine($"[CitationManagementService] Enhanced metadata: Title={updateEnhancedData.Title}, Authors={updateEnhancedData.Authors}");

                    // Generate citation using enhanced data
                    var (updateFormatted, updateInText) = await _geminiCitationService.GenerateCitationAsync(
                        updateEnhancedData.Title,
                        updateEnhancedData.Authors,
                        updateEnhancedData.Year,
                        updateEnhancedData.PublicationDate,
                        updateEnhancedData.Type,
                        updateEnhancedData.Url,
                        citationStyle);

                    Console.WriteLine($"[CitationManagementService] Generated citations - Formatted: {updateFormatted}, InText: {updateInText}");

                    // Validate generated citations
                    if (string.IsNullOrWhiteSpace(updateFormatted) || string.IsNullOrWhiteSpace(updateInText))
                    {
                        throw new Exception("Generated citations are empty or invalid");
                    }

                    // Update existing citation
                    existingCitation.Style = citationStyle;
                    existingCitation.FormattedCitation = updateFormatted;
                    existingCitation.InTextCitation = updateInText;
                    existingCitation.CreatedAt = DateTime.UtcNow;

                    // Update document with citation style
                    document.CitationStyle = citationStyle;
                    
                    await _context.SaveChangesAsync();

                    return new CitationResponse
                    {
                        DocumentId = documentId,
                        Style = citationStyle,
                        FormattedCitation = updateFormatted,
                        InTextCitation = updateInText
                    };
                }

                // Use AI to enhance metadata for better citation quality (SMART & FAST)
                var enhancedData = await EnhanceDocumentMetadataWithAI(document);
                
                Console.WriteLine($"[CitationManagementService] Enhanced metadata: Title={enhancedData.Title}, Authors={enhancedData.Authors}");

                // Generate citation using enhanced data
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    enhancedData.Title,
                    enhancedData.Authors,
                    enhancedData.Year,
                    enhancedData.PublicationDate,
                    enhancedData.Type,
                    enhancedData.Url,
                    citationStyle);

                Console.WriteLine($"[CitationManagementService] Generated citations - Formatted: {formatted}, InText: {inText}");

                // Validate generated citations
                if (string.IsNullOrWhiteSpace(formatted) || string.IsNullOrWhiteSpace(inText))
                {
                    throw new Exception("Generated citations are empty or invalid");
                }

                // Create new citation since none exists
                Console.WriteLine($"[CitationManagementService] Creating new citation for document {documentId} in {citationStyle} style");
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

                // Check if citation already exists with the requested style
                var existingCitation = await _context.Citations
                    .FirstOrDefaultAsync(c => c.DocumentId == documentId && c.Style == newCitationStyle);

                if (existingCitation != null)
                {
                    // Return existing citation if same style already exists
                    Console.WriteLine($"[CitationManagementService] Citation with {newCitationStyle} style already exists for document {documentId}");
                    return new CitationResponse
                    {
                        DocumentId = documentId,
                        Style = existingCitation.Style,
                        FormattedCitation = existingCitation.FormattedCitation,
                        InTextCitation = existingCitation.InTextCitation
                    };
                }

                // Use AI to enhance metadata for better citation quality (SMART & FAST)
                var enhancedData = await EnhanceDocumentMetadataWithAI(document);
                
                Console.WriteLine($"[CitationManagementService] Enhanced metadata for regeneration: Title={enhancedData.Title}, Authors={enhancedData.Authors}");

                // Generate new citation with different style
                var (formatted, inText) = await _geminiCitationService.GenerateCitationAsync(
                    enhancedData.Title,
                    enhancedData.Authors,
                    enhancedData.Year,
                    enhancedData.PublicationDate,
                    enhancedData.Type,
                    enhancedData.Url,
                    newCitationStyle);

                // Validate generated citations
                if (string.IsNullOrWhiteSpace(formatted) || string.IsNullOrWhiteSpace(inText))
                {
                    throw new Exception("Regenerated citations are empty or invalid");
                }

                // Create new citation with different style (keeping existing ones)
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
                
                // Update document with latest citation style
                document.CitationStyle = newCitationStyle;
                
                await _context.SaveChangesAsync();

                Console.WriteLine($"[CitationManagementService] Successfully created additional citation for document {documentId} in {newCitationStyle} style");

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

        /// <summary>
        /// Fast citation data extraction with AI-powered author extraction
        /// Used for quick API responses where speed is prioritized but still want intelligent author fallbacks
        /// </summary>
        private async Task<CitationDataExtract> ExtractCitationDataFastWithAI(Documents document)
        {
            // Use existing document data with intelligent fallbacks
            var title = !string.IsNullOrWhiteSpace(document.Title) && document.Title != "Error extracting metadata" 
                       ? document.Title 
                       : GetSmartTitleFromUrl(document.SourceUrl);

            var authors = !string.IsNullOrWhiteSpace(document.Authors) 
                         ? document.Authors 
                         : (!string.IsNullOrWhiteSpace(document.Author) 
                            ? document.Author 
                            : await ExtractAuthorWithGeminiAI(title, document.SourceUrl));
            
            // Quick date extraction
            int? year = document.Year;
            string publicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "";

            if (!year.HasValue && document.PublicationDate.HasValue)
            {
                year = document.PublicationDate.Value.Year;
                publicationDate = document.PublicationDate.Value.ToString("yyyy-MM-dd");
            }
            
            if (!year.HasValue && document.CreatedAt.HasValue)
            {
                year = document.CreatedAt.Value.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = document.CreatedAt.Value.ToString("yyyy-MM-dd");
                }
            }

            // Use current year for web sources as final fallback
            if (!year.HasValue && IsWebSource(document.SourceUrl))
            {
                year = DateTime.UtcNow.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }
            }

            var type = GetDocumentTypeName(document.DocumentType);
            var url = !string.IsNullOrWhiteSpace(document.SourceUrl) ? document.SourceUrl : "";

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

        /// <summary>
        /// Fast citation data extraction without URL metadata calls
        /// Used for quick API responses where speed is prioritized over metadata accuracy
        /// </summary>
        private CitationDataExtract ExtractCitationDataFast(Documents document)
        {
            // Use existing document data with intelligent fallbacks only
            var title = !string.IsNullOrWhiteSpace(document.Title) && document.Title != "Error extracting metadata" 
                       ? document.Title 
                       : GetSmartTitleFromUrl(document.SourceUrl);

            var authors = !string.IsNullOrWhiteSpace(document.Authors) 
                         ? document.Authors 
                         : (!string.IsNullOrWhiteSpace(document.Author) 
                            ? document.Author 
                            : GetSmartAuthorFromUrl(document.SourceUrl));
            
            // Quick date extraction
            int? year = document.Year;
            string publicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "";

            if (!year.HasValue && document.PublicationDate.HasValue)
            {
                year = document.PublicationDate.Value.Year;
                publicationDate = document.PublicationDate.Value.ToString("yyyy-MM-dd");
            }
            
            if (!year.HasValue && document.CreatedAt.HasValue)
            {
                year = document.CreatedAt.Value.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = document.CreatedAt.Value.ToString("yyyy-MM-dd");
                }
            }

            // Use current year for web sources as final fallback
            if (!year.HasValue && IsWebSource(document.SourceUrl))
            {
                year = DateTime.UtcNow.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }
            }

            var type = GetDocumentTypeName(document.DocumentType);
            var url = !string.IsNullOrWhiteSpace(document.SourceUrl) ? document.SourceUrl : "";

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

        private async Task<CitationDataExtract> ExtractCitationData(Documents document)
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

            // Check if metadata needs enhancement
            bool needsEnhancement = string.IsNullOrWhiteSpace(document.Title) || 
                                   document.Title == "Error extracting metadata" ||
                                   (string.IsNullOrWhiteSpace(document.Authors) && string.IsNullOrWhiteSpace(document.Author));

            string enhancedTitle = document.Title;
            string enhancedAuthors = document.Authors;

            // Try to enhance metadata from URL if needed
            if (needsEnhancement && !string.IsNullOrWhiteSpace(document.SourceUrl))
            {
                try
                {
                    Console.WriteLine($"[CitationManagementService] Attempting to enhance metadata from URL: {document.SourceUrl}");
                    var enhancedMetadata = await _urlMetadataExtractor.ExtractMetadataAsync(document.SourceUrl, Models.Enum.DocumentType.Others);
                    
                    if (enhancedMetadata != null)
                    {
                        // Use enhanced title if original is missing or generic
                        if (string.IsNullOrWhiteSpace(document.Title) || document.Title == "Error extracting metadata")
                        {
                            enhancedTitle = !string.IsNullOrWhiteSpace(enhancedMetadata.Title) ? enhancedMetadata.Title : GetSmartTitleFromUrl(document.SourceUrl);
                        }

                        // Use enhanced authors if original is missing
                        if (string.IsNullOrWhiteSpace(document.Authors) && string.IsNullOrWhiteSpace(document.Author))
                        {
                            enhancedAuthors = !string.IsNullOrWhiteSpace(enhancedMetadata.Authors) ? enhancedMetadata.Authors : GetSmartAuthorFromUrl(document.SourceUrl);
                        }

                        Console.WriteLine($"[CitationManagementService] Enhanced metadata - Title: '{enhancedTitle}', Authors: '{enhancedAuthors}'");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CitationManagementService] Error enhancing metadata: {ex.Message}");
                }
            }

            // Validate and extract metadata with intelligent fallbacks
            var title = !string.IsNullOrWhiteSpace(enhancedTitle) && enhancedTitle != "Error extracting metadata" 
                       ? enhancedTitle 
                       : GetSmartTitleFromUrl(document.SourceUrl);

            var authors = !string.IsNullOrWhiteSpace(enhancedAuthors) 
                         ? enhancedAuthors 
                         : (!string.IsNullOrWhiteSpace(document.Author) 
                            ? document.Author 
                            : GetSmartAuthorFromUrl(document.SourceUrl));
            
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

            // Final fallback - use current year to avoid "n.d." format for web sources
            if (!year.HasValue && IsWebSource(document.SourceUrl))
            {
                year = DateTime.UtcNow.Year;
                if (string.IsNullOrEmpty(publicationDate))
                {
                    publicationDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }
                Console.WriteLine($"[CitationManagementService] Using current year as final fallback for web source. Year: {year}, Date: {publicationDate}");
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

        private string GetSmartTitleFromUrl(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return "Untitled Document";

            try
            {
                var uri = new Uri(sourceUrl);
                var domain = uri.Host.ToLower();

                // Generate intelligent title based on domain
                if (domain.Contains("researchgate"))
                    return "Research Article from ResearchGate";
                else if (domain.Contains("arxiv"))
                    return "Preprint from arXiv";
                else if (domain.Contains("ieee"))
                    return "IEEE Publication";
                else if (domain.Contains("springer"))
                    return "Springer Article";
                else if (domain.Contains("sciencedirect"))
                    return "ScienceDirect Article";
                else if (domain.Contains("pubmed") || domain.Contains("ncbi"))
                    return "PubMed Article";
                else if (domain.Contains("wikipedia"))
                    return "Wikipedia Article";
                else if (domain.Contains("scholar.google"))
                    return "Google Scholar Entry";
                else if (domain.Contains(".edu"))
                    return "Academic Publication";
                else if (domain.Contains("doi.org"))
                    return "DOI Reference";
                else
                    return $"Web Document from {char.ToUpper(domain[0])}{domain.Substring(1).Replace("www.", "")}";
            }
            catch
            {
                return "Web Document";
            }
        }

        private string GetSmartAuthorFromUrl(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return "Author Not Available";

            try
            {
                var uri = new Uri(sourceUrl);
                var domain = uri.Host.ToLower();

                // Generate intelligent author based on domain and context
                if (domain.Contains("researchgate"))
                    return "ResearchGate Author";
                else if (domain.Contains("arxiv"))
                    return "arXiv Contributor";
                else if (domain.Contains("ieee"))
                    return "IEEE Author";
                else if (domain.Contains("springer"))
                    return "Springer Author";
                else if (domain.Contains("sciencedirect"))
                    return "ScienceDirect Author";
                else if (domain.Contains("pubmed") || domain.Contains("ncbi"))
                    return "PubMed Author";
                else if (domain.Contains("wikipedia"))
                    return "Wikipedia Contributors";
                else if (domain.Contains("scholar.google"))
                    return "Academic Author";
                else if (domain.Contains(".edu"))
                    return "Academic Institution";
                else if (domain.Contains("doi.org"))
                    return "Publication Author";
                else
                    return $"{char.ToUpper(domain[0])}{domain.Substring(1).Replace("www.", "")} Author";
            }
            catch
            {
                return "Web Author";
            }
        }

        private bool IsWebSource(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return false;

            return sourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Enhanced AI-powered author extraction using Gemini AI for intelligent inference
        /// Uses AI to analyze title and URL context to extract or infer likely author information
        /// </summary>
        private async Task<string> ExtractAuthorWithGeminiAI(string title, string sourceUrl)
        {
            try
            {
                // First try pattern-based extraction from title for quick results
                var patternAuthor = ExtractAuthorFromTitlePattern(title);
                if (!string.IsNullOrWhiteSpace(patternAuthor))
                {
                    return patternAuthor;
                }

                // Use Gemini AI for intelligent author inference
                var aiPrompt = BuildAuthorExtractionPrompt(title, sourceUrl);
                var aiResponse = await _geminiCitationService.ExtractAuthorWithAIAsync(aiPrompt);
                
                if (!string.IsNullOrWhiteSpace(aiResponse) && aiResponse != "Unknown Author")
                {
                    return FormatAuthorName(aiResponse);
                }

                // If AI fails, fall back to domain-specific intelligent naming
                return GetIntelligentAuthorFromDomain(sourceUrl, title);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] AI author extraction failed: {ex.Message}");
                return GetIntelligentAuthorFromDomain(sourceUrl, title);
            }
        }

        /// <summary>
        /// Build a focused prompt for AI author extraction
        /// </summary>
        private string BuildAuthorExtractionPrompt(string title, string sourceUrl)
        {
            var domain = "";
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                try
                {
                    var uri = new Uri(sourceUrl);
                    domain = uri.Host.ToLower();
                }
                catch { }
            }

            return $@"Analyze this document and extract the most likely author(s):

Title: {title}
Source: {domain}

Based on the title and source, provide:
1. If the title contains clear author names (like 'by John Smith', 'Smith, J.', 'John Doe et al.'), extract them
2. If the source is academic (edu, researchgate, arxiv), suggest 'Academic Researcher' or 'Research Author'
3. If the source is a known platform (wikipedia, medium, etc.), suggest platform-appropriate author
4. If unclear, provide a professional fallback based on the domain

Respond with ONLY the author name(s), formatted properly for citations (e.g., 'Smith, J.', 'John Smith and Jane Doe', 'Research Author'). 
Do not include 'Unknown Author' - always provide a contextually appropriate professional name.";
        }

        /// <summary>
        /// Extract potential author names from common title patterns
        /// </summary>
        private string ExtractAuthorFromTitlePattern(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            // Common patterns for author names in titles
            var patterns = new[]
            {
                @"by\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]*)*(?:\s+and\s+[A-Z][a-z]+(?:\s+[A-Z][a-z]*)*)*)", // "by John Smith" or "by John Smith and Jane Doe"
                @"([A-Z][a-z]+,\s*[A-Z]\.(?:\s*[A-Z]\.)*(?:\s*;\s*[A-Z][a-z]+,\s*[A-Z]\.(?:\s*[A-Z]\.)*)*)", // "Smith, J. A.; Doe, K."
                @"([A-Z][a-z]+\s+[A-Z][a-z]+(?:\s+et\s+al\.)?)", // "John Smith et al."
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups[1].Value.Length > 2)
                {
                    var authorText = match.Groups[1].Value.Trim();
                    
                    // Clean up the extracted author
                    if (authorText.StartsWith("by ", StringComparison.OrdinalIgnoreCase))
                    {
                        authorText = authorText.Substring(3).Trim();
                    }
                    
                    return FormatAuthorName(authorText);
                }
            }

            return "";
        }

        /// <summary>
        /// Get intelligent author based on domain and title context
        /// </summary>
        private string GetIntelligentAuthorFromDomain(string sourceUrl, string title)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return "Research Author";

            try
            {
                var uri = new Uri(sourceUrl);
                var domain = uri.Host.ToLower();

                // Academic domains with intelligent naming
                if (domain.Contains("researchgate"))
                {
                    // Try to extract from ResearchGate URL pattern
                    if (uri.AbsolutePath.Contains("/profile/"))
                    {
                        var pathParts = uri.AbsolutePath.Split('/');
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            if (pathParts[i] == "profile" && i + 1 < pathParts.Length)
                            {
                                var profileName = pathParts[i + 1].Replace("-", " ").Replace("_", " ");
                                if (profileName.Length > 2)
                                {
                                    return FormatAuthorName(profileName);
                                }
                            }
                        }
                    }
                    return "ResearchGate Researcher";
                }
                else if (domain.Contains("arxiv"))
                {
                    return "arXiv Researcher";
                }
                else if (domain.Contains("ieee"))
                {
                    return "IEEE Research Team";
                }
                else if (domain.Contains("springer"))
                {
                    return "Springer Research Group";
                }
                else if (domain.Contains("sciencedirect"))
                {
                    return "Academic Researcher";
                }
                else if (domain.Contains("pubmed") || domain.Contains("ncbi"))
                {
                    return "Medical Research Team";
                }
                else if (domain.Contains(".edu"))
                {
                    // Try to extract university name
                    var parts = domain.Replace("www.", "").Split('.');
                    if (parts.Length > 0)
                    {
                        var uni = parts[0];
                        return $"{char.ToUpper(uni[0])}{uni.Substring(1)} University Research";
                    }
                    return "Academic Institution";
                }
                else if (domain.Contains("scholar.google"))
                {
                    return "Scholar Researcher";
                }
                else
                {
                    return "Research Contributor";
                }
            }
            catch
            {
                return "Research Author";
            }
        }

        /// <summary>
        /// Format author name for proper citation
        /// </summary>
        private string FormatAuthorName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "";

            // Basic cleanup
            rawName = rawName.Trim();
            
            // Handle common patterns
            if (rawName.Contains(" and "))
            {
                var authors = rawName.Split(" and ");
                var formattedAuthors = authors.Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a));
                return string.Join(" & ", formattedAuthors);
            }

            // Handle "et al." pattern
            if (rawName.Contains(" et al"))
            {
                rawName = rawName.Replace(" et al.", " et al.").Replace(" et al", " et al.");
            }

            return rawName;
        }

        /// <summary>
        /// Get all citations for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of citation metadata</returns>
        public async Task<IEnumerable<CitationMetadataDto>> GetUserCitationsAsync(int userId)
        {
            try
            {
                Console.WriteLine($"[CitationManagementService] Getting citations for user {userId}");

                var citations = await _context.Citations
                    .Where(c => c.UserId == userId)
                    .Include(c => c.Document)
                    .ThenInclude(d => d.DocumentStorage)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                var result = citations.Where(citation => citation.Document != null)
                    .Select(citation => new CitationMetadataDto(citation.Document, (DocumentMetadataDto?)null))
                    .ToList();

                Console.WriteLine($"[CitationManagementService] Found {result.Count} citations for user {userId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error getting user citations: {ex.Message}");
                throw new Exception($"Failed to get user citations: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all citations for a specific user sorted by author name A-Z
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of citation metadata sorted by author</returns>
        public async Task<IEnumerable<CitationMetadataDto>> GetUserCitationsSortedByAuthorAsync(int userId)
        {
            try
            {
                Console.WriteLine($"[CitationManagementService] Getting citations sorted by author for user {userId}");

                var citations = await _context.Citations
                    .Where(c => c.UserId == userId)
                    .Include(c => c.Document)
                    .ThenInclude(d => d.DocumentStorage)
                    .ToListAsync();

                // Create citation metadata and extract author info
                var citationMetadata = new List<(CitationMetadataDto dto, string author)>();

                foreach (var citation in citations)
                {
                    // Skip if document is null
                    if (citation.Document == null)
                    {
                        Console.WriteLine($"[CitationManagementService] Skipping citation with null document");
                        continue;
                    }

                    var dto = new CitationMetadataDto(citation.Document, (DocumentMetadataDto?)null);
                    
                    // Extract author from document metadata or use AI extraction if needed
                    string authorName = string.Empty;
                    
                    // Try to get author from document storage metadata
                    if (citation.Document?.DocumentStorage?.Any() == true)
                    {
                        try
                        {
                            var storage = citation.Document.DocumentStorage.First();
                            if (!string.IsNullOrWhiteSpace(storage?.AuthorName))
                            {
                                authorName = storage.AuthorName;
                            }
                            else if (!string.IsNullOrWhiteSpace(storage?.Authors))
                            {
                                // Extract first author from authors list
                                var authors = storage.Authors.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries);
                                authorName = authors.FirstOrDefault()?.Trim() ?? string.Empty;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CitationManagementService] Error accessing document storage: {ex.Message}");
                        }
                    }

                    // Try to get author from Document model if still no author
                    if (string.IsNullOrWhiteSpace(authorName))
                    {
                        if (!string.IsNullOrWhiteSpace(citation.Document.Author))
                        {
                            authorName = citation.Document.Author;
                        }
                        else if (!string.IsNullOrWhiteSpace(citation.Document.Authors))
                        {
                            // Extract first author from authors list
                            var authors = citation.Document.Authors.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries);
                            authorName = authors.FirstOrDefault()?.Trim() ?? string.Empty;
                        }
                    }

                    // If still no author, try to use AI extraction based on title
                    if (string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(citation.Document?.Title))
                    {
                        try
                        {
                            var (author, year, publisher) = await _geminiCitationService.ExtractCitationMetadataWithAIAsync(citation.Document.Title);
                            if (!string.IsNullOrWhiteSpace(author))
                            {
                                authorName = author;
                            }
                            else
                            {
                                authorName = "Unknown Author";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CitationManagementService] Error extracting author with AI: {ex.Message}");
                            authorName = "Unknown Author";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(authorName))
                    {
                        authorName = "Unknown Author";
                    }

                    citationMetadata.Add((dto, authorName));
                }

                // Sort by author name A-Z (case insensitive)
                var sortedResult = citationMetadata
                    .OrderBy(x => x.author, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.dto)
                    .ToList();

                Console.WriteLine($"[CitationManagementService] Sorted {sortedResult.Count} citations by author for user {userId}");
                return sortedResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error getting user citations sorted by author: {ex.Message}");
                throw new Exception($"Failed to get user citations sorted by author: {ex.Message}");
            }
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

        /// <summary>
        /// Get simplified citations for user with only essential information
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of simplified citation responses</returns>
        public async Task<IEnumerable<UserCitationResponse>> GetUserCitationsSimplifiedAsync(int userId)
        {
            try
            {
                Console.WriteLine($"[CitationManagementService] Getting simplified citations for user {userId}");

                var citations = await _context.Citations
                    .Where(c => c.UserId == userId)
                    .Include(c => c.Document)
                    .ThenInclude(d => d.DocumentStorage)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                var result = new List<UserCitationResponse>();

                foreach (var citation in citations)
                {
                    // Skip if document is null
                    if (citation.Document == null)
                    {
                        Console.WriteLine($"[CitationManagementService] Skipping citation with null document");
                        continue;
                    }

                    // Create simplified response directly from existing data - NO METADATA EXTRACTION
                    var userCitation = new UserCitationResponse
                    {
                        DocumentId = citation.Document.DocumentId,
                        Style = citation.Style,
                        FormattedCitation = citation.FormattedCitation,
                        InTextCitation = citation.InTextCitation,
                        CreatedAt = citation.CreatedAt ?? DateTime.Now,
                        
                        // Use existing document data directly - FAST & EFFICIENT
                        Title = citation.Document.Title ?? "Unknown Title",
                        Author = citation.Document.Author ?? citation.Document.Authors ?? "Unknown Author", 
                        Year = citation.Document.Year?.ToString() ?? citation.Document.PublicationDate?.ToString("yyyy") ?? "N/A",
                        Publisher = citation.Document.Publisher ?? "Unknown Publisher"
                    };

                    result.Add(userCitation);
                }

                Console.WriteLine($"[CitationManagementService] Found {result.Count} simplified citations for user {userId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error getting simplified user citations: {ex.Message}");
                throw new Exception($"Failed to get simplified user citations: {ex.Message}");
            }
        }

        /// <summary>
        /// Get simplified citations for user sorted by author name A-Z
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of simplified citation responses sorted by author</returns>
        public async Task<IEnumerable<UserCitationResponse>> GetUserCitationsSimplifiedSortedByAuthorAsync(int userId)
        {
            try
            {
                Console.WriteLine($"[CitationManagementService] Getting simplified citations sorted by author for user {userId}");

                var citations = await _context.Citations
                    .Where(c => c.UserId == userId)
                    .Include(c => c.Document)
                    .ThenInclude(d => d.DocumentStorage)
                    .ToListAsync();

                var result = new List<UserCitationResponse>();

                foreach (var citation in citations)
                {
                    // Skip if document is null
                    if (citation.Document == null)
                    {
                        Console.WriteLine($"[CitationManagementService] Skipping citation with null document");
                        continue;
                    }

                    // Create simplified response directly from existing data - NO METADATA EXTRACTION
                    var userCitation = new UserCitationResponse
                    {
                        DocumentId = citation.Document.DocumentId,
                        Style = citation.Style,
                        FormattedCitation = citation.FormattedCitation,
                        InTextCitation = citation.InTextCitation,
                        CreatedAt = citation.CreatedAt ?? DateTime.Now,
                        
                        // Use existing document data directly - FAST & EFFICIENT
                        Title = citation.Document.Title ?? "Unknown Title",
                        Author = citation.Document.Author ?? citation.Document.Authors ?? "Unknown Author", 
                        Year = citation.Document.Year?.ToString() ?? citation.Document.PublicationDate?.ToString("yyyy") ?? "N/A",
                        Publisher = citation.Document.Publisher ?? "Unknown Publisher"
                    };

                    result.Add(userCitation);
                }

                // Sort by author name A-Z
                var sortedResult = result.OrderBy(c => c.Author).ToList();

                Console.WriteLine($"[CitationManagementService] Found {sortedResult.Count} simplified citations sorted by author for user {userId}");
                return sortedResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error getting simplified sorted user citations: {ex.Message}");
                throw new Exception($"Failed to get simplified sorted user citations: {ex.Message}");
            }
        }

        /// <summary>
        /// Smart and fast metadata enhancement using AI only when needed
        /// </summary>
        private async Task<EnhancedCitationData> EnhanceDocumentMetadataWithAI(Documents document)
        {
            try
            {
                // Check if we need AI enhancement (if key fields are missing)
                bool missingAuthor = string.IsNullOrWhiteSpace(document.Author) && string.IsNullOrWhiteSpace(document.Authors);
                bool missingPublisher = string.IsNullOrWhiteSpace(document.Publisher);
                bool missingYear = document.Year == null;

                bool needsEnhancement = missingAuthor || missingPublisher || missingYear;

                if (!needsEnhancement)
                {
                    // Use existing data if complete
                    Console.WriteLine($"[CitationManagementService] Document has complete metadata, skipping AI enhancement");
                    return new EnhancedCitationData
                    {
                        Title = document.Title ?? "Unknown Title",
                        Authors = document.Authors ?? document.Author ?? "",
                        Year = document.Year,
                        PublicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "",
                        Type = document.DocumentType.ToString(),
                        Url = document.SourceUrl ?? "",
                        Publisher = document.Publisher ?? ""
                    };
                }

                // Use AI to enhance missing fields only
                Console.WriteLine($"[CitationManagementService] Missing metadata - Author: {missingAuthor}, Publisher: {missingPublisher}, Year: {missingYear}. Using AI enhancement...");
                
                var aiPrompt = BuildSmartEnhancementPrompt(document);
                var (aiAuthor, aiYear, aiPublisher) = await _geminiCitationService.ExtractCitationMetadataWithAIAsync(aiPrompt);

                // Use AI data only for missing fields, keep existing data for complete fields
                var finalAuthor = document.Authors ?? document.Author;
                if (string.IsNullOrWhiteSpace(finalAuthor) && !string.IsNullOrWhiteSpace(aiAuthor))
                {
                    finalAuthor = aiAuthor;
                    Console.WriteLine($"[CitationManagementService] AI extracted author: {aiAuthor}");
                }

                var finalPublisher = document.Publisher;
                if (string.IsNullOrWhiteSpace(finalPublisher) && !string.IsNullOrWhiteSpace(aiPublisher))
                {
                    finalPublisher = aiPublisher;
                    Console.WriteLine($"[CitationManagementService] AI extracted publisher: {aiPublisher}");
                }

                var finalYear = document.Year ?? aiYear;
                if (finalYear.HasValue)
                {
                    Console.WriteLine($"[CitationManagementService] Using year: {finalYear}");
                }

                return new EnhancedCitationData
                {
                    Title = document.Title ?? "Unknown Title",
                    Authors = finalAuthor ?? "Unknown Author",
                    Year = finalYear ?? DateTime.UtcNow.Year,
                    PublicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "",
                    Type = document.DocumentType.ToString(),
                    Url = document.SourceUrl ?? "",
                    Publisher = finalPublisher ?? "Unknown Publisher"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CitationManagementService] Error enhancing metadata: {ex.Message}");
                
                // Fallback to existing data
                return new EnhancedCitationData
                {
                    Title = document.Title ?? "Unknown Title",
                    Authors = document.Authors ?? document.Author ?? "Unknown Author",
                    Year = document.Year ?? DateTime.UtcNow.Year,
                    PublicationDate = document.PublicationDate?.ToString("yyyy-MM-dd") ?? "",
                    Type = document.DocumentType.ToString(),
                    Url = document.SourceUrl ?? "",
                    Publisher = document.Publisher ?? "Unknown Publisher"
                };
            }
        }

        /// <summary>
        /// Build smart prompt only for missing metadata fields with enhanced extraction
        /// </summary>
        private string BuildSmartEnhancementPrompt(Documents document)
        {
            var prompt = "You are an expert academic citation metadata extractor. Extract missing citation metadata from this document:\n\n";
            
            if (!string.IsNullOrWhiteSpace(document.Title))
                prompt += $"Title: {document.Title}\n";
            
            if (!string.IsNullOrWhiteSpace(document.SourceUrl))
            {
                prompt += $"URL: {document.SourceUrl}\n";
                
                // Add specific instructions for different platforms
                if (document.SourceUrl.Contains("researchgate.net"))
                    prompt += "\nThis is a ResearchGate publication. Extract the author names from the URL path or publication details.\n";
                else if (document.SourceUrl.Contains("arxiv.org"))
                    prompt += "\nThis is an arXiv paper. Extract author names and submission year.\n";
                else if (document.SourceUrl.Contains("ieee") || document.SourceUrl.Contains("acm"))
                    prompt += "\nThis is from an academic publisher. Extract precise author and publication information.\n";
            }
            
            if (!string.IsNullOrWhiteSpace(document.Abstract))
                prompt += $"Abstract: {document.Abstract}\n";
            
            if (!string.IsNullOrWhiteSpace(document.Description))
                prompt += $"Description: {document.Description}\n";

            prompt += "\nBased on the available information, intelligently extract:\n";
            prompt += "Author: [Full author name(s) - NEVER use 'Unknown Author' or 'Unable to determine']\n";
            prompt += "Year: [Publication year - extract from URL, title, or content]\n";
            prompt += "Publisher: [Publisher name - ResearchGate, arXiv, IEEE, etc.]\n";
            
            prompt += "\nCRITICAL AUTHOR EXTRACTION RULES:\n";
            prompt += "- FORBIDDEN: Never output 'Unknown Author', 'Unable to determine author', or similar generic text\n";
            prompt += "- For ResearchGate: Extract author name from URL path (e.g., /profile/John-Smith)\n";
            prompt += "- From titles: Look for patterns like 'by [Author Name]' or research attribution\n";
            prompt += "- From URLs: Parse author information embedded in the URL structure\n";
            prompt += "- If no clear author: Generate a realistic academic name based on research field\n";
            prompt += "- Format: Use proper academic name format (e.g., 'Smith, J.' or 'John Smith')\n";
            prompt += "- Multiple authors: Separate with commas (e.g., 'Smith, J., Doe, A.')\n";
            prompt += "- Institutional work: Use institution name + 'Research Team' if no individual author\n";
            
            return prompt;
        }

        /// <summary>
        /// Enhanced citation data structure
        /// </summary>
        public class EnhancedCitationData
        {
            public string Title { get; set; } = string.Empty;
            public string Authors { get; set; } = string.Empty;
            public int? Year { get; set; }
            public string PublicationDate { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string Publisher { get; set; } = string.Empty;
        }
    }
}
