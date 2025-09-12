using iTextSharp.text.pdf;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.ExtendedProperties;
using System.Text.RegularExpressions;
using VUniBox.Models.DTO.Response;
using VUniBox.Models.DTO;
using DocumentFormat.OpenXml.CustomProperties;
using System.Xml.Linq;

namespace VUniBox.Services.Metadata
{
    /// <summary>
    /// Service for extracting metadata from various file types.
    /// </summary>
    public class FileMetadataExtractor : IFileMetadataExtractor
    {
        /// <summary>
        /// Extracts metadata from a file based on its extension.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <param name="fileName">The name of the file (including extension).</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata.</returns>
        public async Task<Models.DTO.DocumentMetadataDto> ExtractMetadataAsync(string filePath, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            
            return extension switch
            {
                ".pdf" => await ExtractFromPdfAsync(filePath),
                ".docx" or ".doc" => await ExtractFromWordAsync(filePath),
                ".txt" or ".md" => await ExtractFromTextAsync(filePath),
                _ => new Models.DTO.DocumentMetadataDto
                {
                    FilePath = filePath,
                    FileType = extension,
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    Description = "File type not supported for metadata extraction"
                }
            };
        }

        /// <summary>
        /// Extracts metadata specifically from a PDF file.
        /// </summary>
        /// <param name="filePath">The full path to the PDF file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the PDF.</returns>
        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromPdfAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = ".pdf",
                RetrievedDate = DateTime.UtcNow
            };

            try
            {
                using var reader = new PdfReader(filePath);
                var info = reader.Info;

                // Extract basic metadata from PDF properties
                metadata.Title = GetPdfProperty(info, "Title") ?? Path.GetFileNameWithoutExtension(filePath);
                metadata.Author = GetPdfProperty(info, "Author") ?? "";
                metadata.Subject = GetPdfProperty(info, "Subject") ?? "";
                metadata.Keywords = GetPdfProperty(info, "Keywords") ?? "";

                // Extract creation date
                var creationDate = GetPdfProperty(info, "CreationDate");
                if (!string.IsNullOrEmpty(creationDate))
                {
                    // PDF dates are in format "D:YYYYMMDDHHmmSSOHH'mm'"
                    var cleanDate = Regex.Replace(creationDate, @"D:(\d{8})", "$1");
                    if (DateTime.TryParseExact(cleanDate.Substring(0, Math.Min(8, cleanDate.Length)), 
                                             "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        metadata.PublicationDate = DateOnly.FromDateTime(date);
                    }
                }

                // Extract creator/producer as publisher fallback
                var creator = GetPdfProperty(info, "Creator") ?? "";
                var producer = GetPdfProperty(info, "Producer") ?? "";
                if (!string.IsNullOrEmpty(creator) && !creator.Contains("Microsoft") && !creator.Contains("Adobe"))
                {
                    metadata.Publisher = creator;
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Extract text content for additional metadata mining
                try
                {
                    var fullText = await ExtractPdfTextAsync(reader);
                    
                    if (!string.IsNullOrEmpty(fullText))
                    {
                        // Fix encoding before extracting metadata
                        fullText = FixVietnameseEncoding(fullText);
                        
                        // Extract metadata from text content
                        ExtractMetadataFromText(metadata, fullText);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue with basic metadata
                    metadata.Description = $"Text extraction failed: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting PDF metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Retrieves a specific property from PDF document information, with Vietnamese encoding fixes.
        /// </summary>
        /// <param name="info">The dictionary containing PDF document information.</param>
        /// <param name="key">The key of the property to retrieve (e.g., "Title", "Author").</param>
        /// <returns>The property value with encoding fixes, or null if the property is not found or empty.</returns>
        private string? GetPdfProperty(Dictionary<string, string> info, string key)
        {
            if (info.ContainsKey(key) && !string.IsNullOrWhiteSpace(info[key]))
            {
                var value = info[key].Trim();
                
                // Fix encoding issues in PDF properties
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        // Common Vietnamese encoding fixes
                        value = value
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại")
                            .Replace("¶", "ả")
                            .Replace("ç", "ề")
                            .Replace("Ç", "Ề")
                            .Replace("ß", "ứ")
                            .Replace("Æ", "Ă");
                            
                        // Try to fix encoding by re-encoding
                        var bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
                        var fixedValue = System.Text.Encoding.UTF8.GetString(bytes);
                        
                        // Only use fixed value if it looks better (contains more Vietnamese chars)
                        if (fixedValue.Contains("ọ") || fixedValue.Contains("á") || fixedValue.Contains("đ") || 
                            fixedValue.Contains("ả") || fixedValue.Contains("ề") || fixedValue.Contains("ứ"))
                        {
                            return fixedValue;
                        }
                    }
                    catch
                    {
                        // If encoding fix fails, return original
                    }
                }
                
                return value;
            }
            return null;
        }

        /// <summary>
        /// Extracts additional metadata from the text content of a document.
        /// </summary>
        /// <param name="metadata">The <see cref="Models.DTO.DocumentMetadataDto"/> object to populate with extracted data.</param>
        /// <param name="text">The full text content of the document.</param>
        private void ExtractMetadataFromText(Models.DTO.DocumentMetadataDto metadata, string text)
        {
            // Extract DOI
            var doiMatch = Regex.Match(text, @"(?:DOI:?\s*)?10\.\d{4,}/[^\s<>""'\]\)]+", RegexOptions.IgnoreCase);
            if (doiMatch.Success)
            {
                metadata.DOI = doiMatch.Value.Replace("DOI:", "").Trim();
            }

            // Extract journal name (look for patterns like "Journal of...", "IEEE...", etc.)
            var journalPatterns = new[]
            {
                @"(?:published in|appears in|from)\s+([A-Z][^.]+(?:Journal|Transactions|Proceedings|Review|Letters)[^.]*)",
                @"(IEEE\s+[^.]+)",
                @"(ACM\s+[^.]+)",
                @"([A-Z][^.]*Journal[^.]*)",
                @"([A-Z][^.]*Proceedings[^.]*)"
            };

            foreach (var pattern in journalPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && string.IsNullOrEmpty(metadata.Journal))
                {
                    metadata.Journal = match.Groups[1].Value.Trim();
                    break;
                }
            }

            // Extract volume, issue, pages
            var volumeMatch = Regex.Match(text, @"(?:Vol\.?\s*|Volume\s+)(\d+)", RegexOptions.IgnoreCase);
            if (volumeMatch.Success)
            {
                metadata.Volume = volumeMatch.Groups[1].Value;
            }

            var issueMatch = Regex.Match(text, @"(?:No\.?\s*|Issue\s+|Number\s+)(\d+)", RegexOptions.IgnoreCase);
            if (issueMatch.Success)
            {
                metadata.Issue = issueMatch.Groups[1].Value;
            }

            var pagesMatch = Regex.Match(text, @"(?:pp\.?\s*|pages?\s*)(\d+)(?:\s*[-��]\s*(\d+))?", RegexOptions.IgnoreCase);
            if (pagesMatch.Success)
            {
                metadata.Pages = pagesMatch.Groups[2].Success 
                    ? $"{pagesMatch.Groups[1].Value}-{pagesMatch.Groups[2].Value}"
                    : pagesMatch.Groups[1].Value;
            }

            // Extract publication year from text if not found in properties
            if (!metadata.PublicationDate.HasValue)
            {
                var yearMatch = Regex.Match(text, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var year))
                {
                    metadata.PublicationDate = new DateOnly(year, 1, 1);
                }
            }

            // Extract publisher
            if (string.IsNullOrEmpty(metadata.Publisher))
            {
                var publisherPatterns = new[]
                {
                    @"(?:Published by|Publisher:)\s*([^.\n]+)",
                    @"(Springer|Elsevier|IEEE|ACM|Nature|Science|Wiley)[^.\n]*",
                    @"([A-Z][^.]*Press[^.]*)",
                    @"([A-Z][^.]*Publications?[^.]*)"
                };

                foreach (var pattern in publisherPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        metadata.Publisher = match.Groups[1].Value.Trim();
                        break;
                    }
                }
            }

            // Extract authors (look for multiple author patterns)
            if (string.IsNullOrEmpty(metadata.Authors))
            {
                var authorPatterns = new[]
                {
                    @"(?:Authors?:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)",
                    @"(?:By:?\s*)([A-Z][^.\n]+(?:,\s*[A-Z][^.\n]+)*)"
                };

                foreach (var pattern in authorPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var authors = match.Groups[1].Value.Trim();
                        metadata.Authors = authors;
                        if (string.IsNullOrEmpty(metadata.Author))
                        {
                            metadata.Author = authors.Split(',')[0].Trim();
                        }
                        break;
                    }
                }
            }

            // Extract abstract
            if (string.IsNullOrEmpty(metadata.Abstract))
            {
                metadata.Abstract = ExtractAbstractFromText(text);
            }

            // Set language
            if (string.IsNullOrEmpty(metadata.Language))
            {
                metadata.Language = "en"; // Default to English for academic papers
            }
        }

        /// <summary>
        /// Extracts metadata specifically from a Word (.docx or .doc) file.
        /// </summary>
        /// <param name="filePath">The full path to the Word file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the Word document.</returns>
        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromWordAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath),
                RetrievedDate = DateTime.UtcNow
            };

            try
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                var mainPart = document.MainDocumentPart;

                // Simple extraction from custom properties
                var customPropsPart = document.CustomFilePropertiesPart;
                if (customPropsPart != null)
                {
                    foreach (var prop in customPropsPart.Properties.Elements<CustomDocumentProperty>())
                    {
                        var name = prop.Name?.Value?.ToLower();
                        var value = prop.VTLPWSTR?.Text;
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            switch (name)
                            {
                                case "title": metadata.Title = value; break;
                                case "author": metadata.Author = value; break;
                                case "authors": metadata.Authors = value; break;
                                case "journal": metadata.Journal = value; break;
                                case "publisher": metadata.Publisher = value; break;
                                case "doi": metadata.DOI = value; break;
                                case "isbn": metadata.ISBN = value; break;
                                case "volume": metadata.Volume = value; break;
                                case "issue": metadata.Issue = value; break;
                                case "pages": metadata.Pages = value; break;
                                case "keywords": metadata.Keywords = value; break;
                                case "subject": metadata.Subject = value; break;
                            }
                        }
                    }
                }

                // Fallback to filename if no title found
                if (string.IsNullOrEmpty(metadata.Title))
                {
                    metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;

                // Extract text content for additional metadata mining
                if (mainPart != null)
                {
                    try
                    {
                        var text = await ExtractWordTextAsync(mainPart);
                        if (!string.IsNullOrEmpty(text))
                        {
                            ExtractMetadataFromText(metadata, text);
                        }
                    }
                    catch (Exception ex)
                    {
                        metadata.Description = $"Text extraction failed: {ex.Message}";
                    }
                }

                // Set default language
                if (string.IsNullOrEmpty(metadata.Language))
                {
                    metadata.Language = "en";
                }
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting Word metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Extracts metadata specifically from a plain text (.txt or .md) file.
        /// </summary>
        /// <param name="filePath">The full path to the text file.</param>
        /// <returns>A <see cref="Models.DTO.DocumentMetadataDto"/> containing the extracted metadata from the text file.</returns>
        public async Task<Models.DTO.DocumentMetadataDto> ExtractFromTextAsync(string filePath)
        {
            var metadata = new Models.DTO.DocumentMetadataDto
            {
                FilePath = filePath,
                FileType = Path.GetExtension(filePath)
            };

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Abstract = ExtractAbstractFromText(content);
                
                // Try to extract basic information from first few lines
                if (lines.Length > 0)
                {
                    metadata.Title = lines[0].Trim();
                }
                
                if (lines.Length > 1)
                {
                    metadata.Author = lines[1].Trim();
                }

                // Get file size
                var fileInfo = new FileInfo(filePath);
                metadata.FileSize = fileInfo.Length;
            }
            catch (Exception ex)
            {
                metadata.Title = Path.GetFileNameWithoutExtension(filePath);
                metadata.Description = $"Error extracting text metadata: {ex.Message}";
            }

            return metadata;
        }

        /// <summary>
        /// Extracts all text content from a PDF reader.
        /// </summary>
        /// <param name="reader">The <see cref="PdfReader"/> instance.</param>
        /// <returns>The concatenated text content from all pages of the PDF.</returns>
        private async Task<string> ExtractPdfTextAsync(PdfReader reader)
        {
            var text = "";
            try
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    var pageText = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
                    
                    // Fix encoding issues by handling UTF-8 properly
                    if (!string.IsNullOrEmpty(pageText))
                    {
                        // Clean up common encoding issues
                        pageText = pageText
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại");
                            
                        // Try to fix more encoding issues
                        var bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(pageText);
                        pageText = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    
                    text += pageText;
                }
            }
            catch (Exception)
            {
                // If encoding fix fails, fallback to original extraction
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text += iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
                }
            }
            return text;
        }

        /// <summary>
        /// Extracts all text content from the main document part of a WordprocessingDocument.
        /// </summary>
        /// <param name="mainPart">The main document part of the WordprocessingDocument.</param>
        /// <returns>The concatenated text content from all paragraphs in the Word document.</returns>
        private async Task<string> ExtractWordTextAsync(MainDocumentPart mainPart)
        {
            if (mainPart?.Document?.Body == null)
                return "";

            var text = "";
            try
            {
                foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                {
                    var paragraphText = paragraph.InnerText;
                    
                    // Fix encoding issues in Word text
                    if (!string.IsNullOrEmpty(paragraphText))
                    {
                        paragraphText = paragraphText
                            .Replace("Õ", "ọ")
                            .Replace("¸", "á")
                            .Replace("®", "đ")
                            .Replace("häc", "học")
                            .Replace("gi¸o", "giáo")
                            .Replace("dôc", "dục")
                            .Replace("hiÖn", "hiện")
                            .Replace("¹i", "ại")
                            .Replace("¶", "ả")
                            .Replace("ç", "ề")
                            .Replace("ß", "ứ");
                    }
                    
                    text += paragraphText + "\n";
                }
            }
            catch (Exception)
            {
                // Fallback to simple extraction
                foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                {
                    text += paragraph.InnerText + "\n";
                }
            }
            
            return text;
        }

        /// <summary>
        /// Extracts an abstract or a summary from the given text content.
        /// Prioritizes sections explicitly marked as "abstract" or similar.
        /// </summary>
        /// <param name="text">The full text content of the document.</param>
        /// <returns>The extracted abstract or a meaningful summary, cleaned and limited in length.</returns>
        private string ExtractAbstractFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Fix encoding issues first
            text = FixVietnameseEncoding(text);
            
            // Clean up extra whitespace and line breaks
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Look for abstract section in multiple languages
            var abstractPatterns = new[]
            {
                @"(?i)abstract[:\s]*(.+?)(?=\n\s*\n|\n\s*[A-Z][a-z]+:|$)",
                @"(?i)tóm\s*tắt[:\s]*(.+?)(?=\n\s*\n|\n\s*[A-Z]|$)",
                @"(?i)tổng\s*quan[:\s]*(.+?)(?=\n\s*\n|\n\s*[A-Z]|$)",
                @"(?i)giới\s*thiệu[:\s]*(.+?)(?=\n\s*\n|\n\s*[A-Z]|$)"
            };

            foreach (var pattern in abstractPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.Singleline);
                if (match.Success)
                {
                    var abstractText = match.Groups[1].Value.Trim();
                    abstractText = CleanAndLimitText(abstractText, 300);
                    return abstractText;
                }
            }

            // Look for introduction or first meaningful paragraph
            var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var meaningfulText = "";
            
            foreach (var sentence in sentences)
            {
                var cleanSentence = sentence.Trim();
                if (cleanSentence.Length > 10 && !IsHeaderOrPageNumber(cleanSentence))
                {
                    meaningfulText += cleanSentence + ". ";
                    if (meaningfulText.Length > 200)
                        break;
                }
            }

            return CleanAndLimitText(meaningfulText, 300);
        }

        /// <summary>
        /// Cleans and limits the length of a given text string.
        /// Removes unwanted characters and ensures the text does not exceed a specified maximum length.
        /// </summary>
        /// <param name="text">The text string to clean and limit.</param>
        /// <param name="maxLength">The maximum desired length of the text.</param>
        /// <returns>The cleaned and length-limited text.</returns>
        private string CleanAndLimitText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            // Remove unwanted characters and patterns
            text = Regex.Replace(text, @"[|\\\/\{\}\[\]<>]", " ");
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            // Limit length and ensure proper ending
            if (text.Length > maxLength)
            {
                var cutPoint = text.LastIndexOf(' ', maxLength - 3);
                if (cutPoint > maxLength / 2)
                {
                    text = text.Substring(0, cutPoint) + "...";
                }
                else
                {
                    text = text.Substring(0, maxLength - 3) + "...";
                }
            }

            return text;
        }

        /// <summary>
        /// Checks if a given text string appears to be a header or a page number.
        /// </summary>
        /// <param name="text">The text string to check.</param>
        /// <returns>True if the text is likely a header or page number, false otherwise.</returns>
        private bool IsHeaderOrPageNumber(string text)
        {
            // Check if text looks like a header, page number, or unwanted content
            return text.Length < 5 ||
                   Regex.IsMatch(text, @"^\d+$") ||
                   Regex.IsMatch(text, @"^(chương|chapter|phần|part)\s*\d+", RegexOptions.IgnoreCase) ||
                   text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || "|-/\\".Contains(c));
        }

        /// <summary>
        /// Attempts to fix common Vietnamese encoding issues in a given text string.
        /// This method addresses specific incorrect character mappings found in some documents.
        /// </summary>
        /// <param name="text">The text string to fix encoding for.</param>
        /// <returns>The text string with common Vietnamese encoding issues resolved.</returns>
        private string FixVietnameseEncoding(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                // Fix common Vietnamese encoding issues from your specific text
                text = text
                    // Remove garbage characters first
                    .Replace("l OM oAR cP SD", "")
                    .Replace("47 20 6 07 1", "")
                    
                    // Major pattern fixes for capital letters
                    .Replace("GIO", "GIÁO")
                    .Replace("TRNH", "TRÌNH") 
                    .Replace("TU?NG", "TƯỞNG")
                    .Replace("H?", "HỒ")
                    .Replace("CH", "CHÍ")
                    .Replace("KHI", "KHÁI")
                    .Replace("NI?M", "NIỆM")
                    .Replace("D?I", "ĐỐI")
                    .Replace("PHP", "PHÁP")
                    .Replace("NGHIN", "NGHIÊN")
                    .Replace("C?U", "CỨU")
                    .Replace("NGHIA", "NGHĨA")
                    .Replace("H?C", "HỌC")
                    .Replace("T?P", "TẬP")
                    .Replace("MN", "MÔN")
                    .Replace("N?I", "NỘI")
                    .Replace("CHUONG", "CHƯƠNG")
                    
                    // Lowercase fixes
                    .Replace("gio", "giáo")
                    .Replace("trnh", "trình")
                    .Replace("tu?ng", "tưởng")
                    .Replace("h?", "hồ")
                    .Replace("ch", "chí")
                    .Replace("khi", "khái")
                    .Replace("ni?m", "niệm")
                    .Replace("d?i", "đối")
                    .Replace("php", "pháp")
                    .Replace("nghin", "nghiên")
                    .Replace("c?u", "cứu")
                    .Replace("nghia", "nghĩa")
                    .Replace("h?c", "học")
                    .Replace("t?p", "tập")
                    .Replace("mn", "môn")
                    .Replace("n?i", "nội")
                    .Replace("chuong", "chương")
                    
                    // Two character combinations
                    .Replace("?i", "ại")
                    .Replace("?u", "ấu")
                    .Replace("?a", "ưa")
                    .Replace("?n", "ần")
                    .Replace("?c", "ức")
                    .Replace("?t", "ột")
                    .Replace("?p", "ập")
                    .Replace("?m", "ầm")
                    .Replace("?g", "ững")
                    .Replace("?r", "ưr")
                    .Replace("?s", "ưs")
                    .Replace("?l", "ưl")
                    .Replace("?k", "ưk")
                    .Replace("?d", "ướd")
                    .Replace("?f", "ướf")
                    .Replace("?v", "ướv")
                    .Replace("?w", "ướw")
                    .Replace("?x", "ướx")
                    .Replace("?y", "ướy")
                    .Replace("?z", "ướz")
                    
                    // Common word patterns
                    .Replace("bi?u", "biểu")
                    .Replace("ton", "toàn")
                    .Replace("qu?c", "quốc")
                    .Replace("l?n", "lần")
                    .Replace("th?", "thế")
                    .Replace("c?a", "của")
                    .Replace("D?ng", "Đảng")
                    .Replace("C?ng", "Cộng")
                    .Replace("s?n", "sản")
                    .Replace("Vi?t", "Việt")
                    .Replace("nu", "nêu")
                    .Replace("như", "như")
                    
                    // Single character replacements
                    .Replace("", "ô")
                    .Replace("?", "ệ")
                    .Replace("Õ", "ọ")
                    .Replace("¸", "á") 
                    .Replace("®", "đ")
                    .Replace("ç", "ề")
                    .Replace("Ç", "Ề")
                    .Replace("ß", "ứ")
                    .Replace("Æ", "Ă")
                    
                    // Additional patterns from the new text
                    .Replace("GIôO", "GIÁO")
                    .Replace("TRôNH", "TRÌNH")
                    .Replace("TUệNG", "TƯỞNG")
                    .Replace("Hệ", "HỒ")
                    .Replace("CHô", "CHÍ")
                    .Replace("KHôI", "KHÁI")
                    .Replace("NIệM", "NIỆM")
                    .Replace("Dệi", "ĐỐI")
                    .Replace("PHôP", "PHÁP")
                    .Replace("NGHIôN", "NGHIÊN")
                    .Replace("Cệu", "CỨU")
                    .Replace("NGHĩA", "NGHĨA")
                    .Replace("Hệc", "HỌC")
                    .Replace("Tệp", "TẬP")
                    .Replace("Môn", "MÔN")
                    .Replace("Nệi", "NỘI");

                // Remove remaining garbage patterns
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[|\\\/\{\}\[\]<>★☆■□●○]", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                text = text.Trim();

                return text;
            }
            catch (Exception)
            {
                return text;
            }
        }
    }
}
