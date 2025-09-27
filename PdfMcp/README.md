# PDF MCP Server

A comprehensive Model Context Protocol (MCP) server that provides complete transparency and control over PDF documents. This server enables AI assistants to open, analyze, search, summarize, and manipulate PDF files with extensive functionality.

## Features

### 🔍 **Complete PDF Analysis**
- Load and parse PDF documents with full metadata extraction
- Support for password-protected PDFs
- Comprehensive document validation and integrity checking
- Multi-library approach for maximum compatibility (PdfPig, iText, PDFsharp)

### 📄 **Content Extraction**
- **Text Extraction**: Full text extraction with preserved formatting
- **Image Extraction**: Extract all embedded images with metadata
- **Metadata Analysis**: Author, title, creation dates, security settings
- **Structure Analysis**: Page dimensions, rotation, annotations, links

### 🔎 **Advanced Search Capabilities**
- **Exact Text Search**: Precise text matching with context
- **Fuzzy Search**: Approximate matching for flexible queries
- **Cross-Document Search**: Search across multiple loaded PDFs
- **Context-Aware Results**: Surrounding text for better understanding

### 📊 **Document Intelligence**
- **Smart Summarization**: AI-powered document summaries
- **Document Comparison**: Compare two PDFs for similarities and differences
- **Structure Analysis**: Word counts, page statistics, content distribution
- **Keyword Frequency**: Most common terms and topics

### 🛠 **Management & Utilities**
- **Memory Management**: Load/unload documents efficiently
- **Batch Operations**: Process multiple documents simultaneously
- **Performance Monitoring**: Memory usage and service statistics
- **Error Handling**: Comprehensive error reporting and recovery

### 🔐 **Security & Validation**
- **Document Validation**: Check PDF integrity and structure
- **Password Support**: Handle encrypted documents securely
- **File Safety**: Validate file types and paths
- **Access Control**: Configurable security settings

## Available Tools

### Document Management
- `LoadPdf` - Load a PDF document into memory
- `GetLoadedDocuments` - List all currently loaded documents
- `GetDocumentInfo` - Get comprehensive document information
- `UnloadDocument` - Remove a document from memory
- `ClearAllDocuments` - Clear all loaded documents

### Content Access
- `GetPageContent` - Get complete content of a specific page
- `ExtractAllText` - Extract full text from all pages
- `GetDocumentMetadata` - Get detailed metadata information
- `ExtractImages` - Extract and save all images from PDF

### Search & Analysis
- `SearchInDocument` - Search for text within a document
- `SearchAcrossDocuments` - Search across multiple documents
- `SummarizeDocument` - Generate intelligent document summary
- `AnalyzeDocumentStructure` - Get structural analysis and statistics

### Utilities
- `CompareDocuments` - Compare two PDF documents
- `ValidatePdf` - Validate PDF integrity and structure
- `GetServiceStatus` - Get memory usage and performance stats

## Quick Start

### 1. Load a PDF Document
```
LoadPdf(filePath: "C:\\Documents\\report.pdf")
```

### 2. Get Document Information
```
GetDocumentInfo(filePath: "C:\\Documents\\report.pdf")
```

### 3. Search for Content
```
SearchInDocument(
    filePath: "C:\\Documents\\report.pdf",
    searchTerm: "financial analysis",
    fuzzySearch: false,
    maxResults: 20
)
```

### 4. Extract All Text
```
ExtractAllText(filePath: "C:\\Documents\\report.pdf")
```

### 5. Generate Summary
```
SummarizeDocument(
    filePath: "C:\\Documents\\report.pdf",
    maxLength: 500
)
```

## Advanced Usage

### Password-Protected PDFs
```
LoadPdf(
    filePath: "C:\\Documents\\secure.pdf",
    password: "your-password"
)
```

### Fuzzy Text Search
```
SearchInDocument(
    filePath: "C:\\Documents\\report.pdf",
    searchTerm: "aproximate text",
    fuzzySearch: true,
    maxResults: 10
)
```

### Document Comparison
```
CompareDocuments(
    filePath1: "C:\\Documents\\version1.pdf",
    filePath2: "C:\\Documents\\version2.pdf"
)
```

### Image Extraction
```
ExtractImages(
    filePath: "C:\\Documents\\presentation.pdf",
    outputDirectory: "C:\\ExtractedImages"
)
```

## Configuration

The server can be configured via `appsettings.json`:

```json
{
  "PdfService": {
    "MaxDocuments": 50,
    "MaxFileSizeMB": 500,
    "EnableOCR": true,
    "OCRLanguages": ["eng"],
    "DefaultSearchContextSize": 100
  },
  "Performance": {
    "EnableParallelProcessing": true,
    "MaxConcurrentOperations": 4,
    "MemoryThresholdMB": 1024
  },
  "Security": {
    "AllowPasswordProtectedPDFs": true,
    "MaxPasswordAttempts": 3,
    "AllowNetworkPaths": false
  }
}
```

## Dependencies

### Core Libraries
- **PdfPig** - Primary PDF reading and text extraction
- **iText 7** - Advanced PDF manipulation and analysis
- **PDFsharp** - Additional PDF processing capabilities
- **TesseractOCR** - OCR for scanned documents
- **SixLabors.ImageSharp** - Image processing
- **FuzzySharp** - Fuzzy text matching

### MCP Framework
- **ModelContextProtocol** - MCP server implementation

## Performance Notes

- **Memory Usage**: Documents are kept in memory for fast access
- **Large Files**: Files over 500MB may require special handling
- **Concurrent Operations**: Up to 4 simultaneous operations supported
- **Caching**: Text and images can be cached for improved performance

## Error Handling

The server provides comprehensive error handling with detailed messages:
- File not found errors
- Password protection issues
- Corrupted PDF detection
- Memory limitations
- Invalid file formats

## Limitations

- Maximum file size: 500MB (configurable)
- Maximum concurrent documents: 50 (configurable)
- OCR requires Tesseract installation for scanned PDFs
- Some heavily corrupted PDFs may not be readable

## Use Cases

### Document Analysis
- Research paper analysis
- Legal document review
- Financial report examination
- Technical manual navigation

### Content Management
- Document archiving
- Text extraction for indexing
- Image asset extraction
- Metadata cataloging

### Quality Assurance
- PDF validation and integrity checking
- Document comparison and versioning
- Structure analysis for compliance

### AI Integration
- Document summarization
- Intelligent search and retrieval
- Content-based recommendations
- Automated document processing

## Support

This MCP server provides complete transparency into PDF documents, enabling AI assistants to work with PDFs as effectively as they work with plain text files. All operations return structured JSON data for easy integration and processing.
