# DocumentServer Consolidation Implementation Plan

**Last Updated:** October 14, 2025  
**Status:** Planning Phase  
**Estimated Effort:** 3-5 conversations  

---

## 📋 Project Standards

### Standard 1: One Class Per File
**Rule:** Only one class is allowed per file.  
**Action:** If the current file being imported into the project has multiple classes, break them up into separate files.  
**Example:** If `Controllers/PdfController.cs` contains request record classes at the bottom, extract them to `Models/Pdf/LoadPdfRequest.cs`, etc.

### Standard 2: Centralized Serialization Options
**Rule:** Use `SerializerOptions` static invocations instead of `new JsonSerializerOptions()`.  
**Location:** `Common/SerializerOptions.cs`  
**Current Options:**
- `SerializerOptions.JsonOptionsIndented` - WriteIndented = true
- `SerializerOptions.JsonOptionsCaseInsensitiveTrue` - PropertyNameCaseInsensitive = true

**Action:** If a different configuration is needed, add a new static property to `SerializerOptions` rather than creating inline options.

### Standard 3: Consistent Folder Structure
**Rule:** Follow the established folder pattern in DocumentServer.  
**Current Structure:**
```
DocumentServer/
├── Common/              # Shared utilities, extensions, options
├── Configuration/       # Settings, options classes
├── Controllers/         # HTTP API endpoints
├── Services/
│   ├── Analysis/       # Document analysis, comparison, validation
│   ├── Core/           # Document loading, caching, extraction
│   ├── DocumentSearch/ # Quick search, fuzzy matching
│   ├── Lucene/         # Full-text indexing, enterprise search
│   └── Ocr/            # OCR detection and processing
└── Models/             # DTOs, request/response models
```

### Standard 4: Logging Best Practices
**Rule:** Unlike STDIO MCP projects, HTTP API servers should have comprehensive logging.  
**Actions:**
- Add `ILogger<T>` injection to all services and controllers
- Log at appropriate levels (Information, Warning, Error)
- Include contextual information (file paths, operation names, durations)
- Log exceptions with stack traces
- Log operation start/completion for debugging

**Example:**
```csharp
logger.LogInformation("Loading document: {FilePath}", filePath);
logger.LogWarning("Password required for encrypted document: {FilePath}", filePath);
logger.LogError(ex, "Failed to extract content from: {FilePath}", filePath);
```

---

## 🎯 Project Overview

### Goal
Consolidate three separate document processing projects into a single, unified DocumentServer:
1. **OfficeReader** - Word, Excel, PowerPoint processing
2. **PdfReader** - PDF processing  
3. **DesktopCommanderMcp/DocumentTools** - Cross-format indexing, search, OCR

### Benefits
- **Token Reduction:** Remove ~15-20 tools from DesktopCommander (18-20% savings)
- **Unified API:** Single endpoint for all document operations
- **Shared Resources:** Single document cache, password manager, search index
- **Enhanced Features:** OCR + Lucene indexing for all formats
- **Maintainability:** One codebase instead of three
- **Performance:** Shared memory pool, optimized caching

---

## 📊 Current State Analysis

### OfficeReader (Port 7030)
**Format Support:** .docx, .doc, .xlsx, .xls, .pptx, .ppt  
**Key Libraries:**
- ClosedXML (Excel)
- DocumentFormat.OpenXml (Office)
- ShapeCrawler (PowerPoint)
- FuzzySharp (fuzzy search)
- MsOfficeCrypto (encryption)

**Features:**
- In-memory document caching
- Format-specific extraction (Excel sheets, PPT slides, Word structure)
- Fuzzy text search
- Cross-document search
- Document comparison
- Validation and analysis

**Service Classes:** `OfficeService.cs` (~800 lines)

---

### PdfReader (Port 7002)
**Format Support:** .pdf  
**Key Libraries:**
- itext (PDF manipulation)
- PdfPig (parsing/extraction)
- SixLabors.ImageSharp (image processing)
- FuzzySharp (fuzzy search)

**Features:**
- In-memory document caching
- Page-level access
- Image extraction
- Document summarization
- Fuzzy text search
- Cross-document search
- Document comparison
- Structure analysis

**Service Classes:** `PdfService.cs` (~700 lines)

---

### DesktopCommanderMcp/DocumentTools (STDIO MCP)
**Format Support:** PDF + Office (both!)  
**Key Libraries:**
- All libraries from Office + PDF readers above
- Lucene.Net (full-text search engine)
- TesseractOCR (OCR)
- NPOI (additional Office support)

**Features:**
- Lucene index creation and management
- Enterprise full-text search
- OCR for scanned PDFs and images
- Password pattern matching
- Auto-password detection
- Index memory management
- Cross-format content extraction

**Service Classes:**
- `DocumentProcessor.cs`
- `DocumentIndexer.cs`
- `PasswordManager.cs`
- `OcrService.cs`

**MCP Tools:** 18 tools exposed via DesktopCommander

---

## 🏗️ Target Architecture

### Unified DocumentServer (Port 7070)

```
DocumentServer/
├── Common/
│   ├── SerializerOptions.cs          # JSON serialization settings
│   ├── DocumentExtensions.cs         # Extension methods
│   └── Constants.cs                   # Shared constants
│
├── Configuration/
│   ├── DocumentServerOptions.cs      # App configuration
│   ├── CacheOptions.cs               # Cache settings
│   └── LuceneOptions.cs              # Index configuration
│
├── Models/
│   ├── Common/
│   │   ├── ServiceResult.cs          # Generic result wrapper
│   │   ├── DocumentInfo.cs           # Document metadata
│   │   ├── DocumentType.cs           # Enum: PDF, Word, Excel, PowerPoint
│   │   └── SearchResult.cs           # Search result model
│   │
│   ├── Requests/
│   │   ├── LoadDocumentRequest.cs
│   │   ├── SearchRequest.cs
│   │   ├── ExtractContentRequest.cs
│   │   ├── CreateIndexRequest.cs
│   │   └── (format-specific requests)
│   │
│   └── Responses/
│       ├── LoadDocumentResult.cs
│       ├── ExtractContentResult.cs
│       ├── SearchResultsResponse.cs
│       └── (format-specific responses)
│
├── Services/
│   ├── Core/
│   │   ├── IDocumentLoader.cs        # Interface for loading
│   │   ├── PdfDocumentLoader.cs      # PDF loading
│   │   ├── OfficeDocumentLoader.cs   # Office loading
│   │   ├── DocumentLoaderFactory.cs  # Factory pattern
│   │   ├── DocumentCache.cs          # In-memory cache
│   │   ├── IContentExtractor.cs      # Interface for extraction
│   │   ├── PdfContentExtractor.cs    # PDF extraction
│   │   ├── OfficeContentExtractor.cs # Office extraction
│   │   └── PasswordManager.cs        # Password management
│   │
│   ├── Analysis/
│   │   ├── DocumentValidator.cs      # Integrity validation
│   │   ├── DocumentComparator.cs     # Document comparison
│   │   ├── StructureAnalyzer.cs      # Structure analysis
│   │   └── MetadataExtractor.cs      # Metadata extraction
│   │
│   ├── DocumentSearch/
│   │   ├── QuickSearchService.cs     # In-memory fuzzy search
│   │   └── FuzzyMatcher.cs           # Fuzzy matching logic
│   │
│   ├── Lucene/
│   │   ├── LuceneIndexer.cs          # Index creation/management
│   │   ├── LuceneSearcher.cs         # Full-text search
│   │   ├── IndexManager.cs           # Index lifecycle
│   │   └── DocumentIndexer.cs        # Document indexing
│   │
│   ├── Ocr/
│   │   ├── OcrService.cs             # OCR orchestration
│   │   ├── TesseractEngine.cs        # Tesseract integration
│   │   └── ImagePreprocessor.cs      # Image enhancement
│   │
│   └── FormatSpecific/
│       ├── Excel/
│       │   ├── ExcelWorksheetReader.cs
│       │   └── ExcelDataExtractor.cs
│       ├── Word/
│       │   ├── WordStructureReader.cs
│       │   └── WordTableExtractor.cs
│       ├── PowerPoint/
│       │   ├── SlideReader.cs
│       │   └── NotesExtractor.cs
│       └── Pdf/
│           ├── PdfPageReader.cs
│           └── PdfImageExtractor.cs
│
└── Controllers/
    ├── ApiDocumentationController.cs  # OpenAPI /description
    ├── DocumentController.cs          # Core operations
    ├── SearchController.cs            # Search operations
    ├── IndexController.cs             # Lucene operations
    ├── OcrController.cs               # OCR operations
    ├── PasswordController.cs          # Password management
    └── FormatSpecificController.cs    # Format-specific endpoints
```

---

## 🔄 Feature Consolidation Matrix

### Core Document Operations

| Feature | Source | Implementation Choice | Rationale |
|---------|--------|---------------------|-----------|
| **Document Loading** | All 3 | Factory pattern with format-specific loaders | Supports all formats with clean abstraction |
| **Password Support** | All 3 | DesktopCommander's PasswordManager | Most advanced: patterns, auto-detection, bulk |
| **Content Extraction** | All 3 | Format-specific extractors with unified interface | Best of both worlds |
| **Document Caching** | Office + PDF | Merge both implementations | Combined approach with configurable size limits |
| **Metadata Extraction** | All 3 | Unified MetadataExtractor | Common interface, format-specific internals |

### Search Capabilities

| Feature | Source | Implementation Choice | Rationale |
|---------|--------|---------------------|-----------|
| **Quick Search** | Office + PDF | Keep both, unified in QuickSearchService | In-memory for loaded documents |
| **Fuzzy Search** | Office + PDF | FuzzySharp library | Already used consistently |
| **Lucene Indexing** | DesktopCommander | Keep implementation | Unique feature, enterprise-grade |
| **Cross-format Search** | DesktopCommander | Keep implementation | Critical consolidation benefit |

### Analysis Features

| Feature | Source | Implementation Choice | Rationale |
|---------|--------|---------------------|-----------|
| **Validation** | Office + PDF | Merge into DocumentValidator | Combine validation logic |
| **Comparison** | Office + PDF | Merge into DocumentComparator | Unified comparison interface |
| **Structure Analysis** | PDF | Keep PdfReader implementation | Comprehensive structure parsing |
| **Summarization** | PDF | Port to unified service | Useful for all formats |

### Unique Features

| Feature | Source | Keep/Port | Rationale |
|---------|--------|-----------|-----------|
| **OCR** | DesktopCommander | Keep | Unique capability, add to all formats |
| **Excel-specific** | OfficeReader | Keep | Format-specific, powerful features |
| **PowerPoint slides** | OfficeReader | Keep | Format-specific navigation |
| **Word structure** | OfficeReader | Keep | Format-specific operations |
| **PDF pages** | PdfReader | Keep | Format-specific navigation |
| **Image extraction** | PdfReader | Keep | Useful feature |

---

## 📝 Implementation Plan

### Phase 1: Project Setup & Core Infrastructure (Conversation 1)

**Goal:** Establish project foundation, core models, and base services

#### Step 1.1: Update Project Dependencies
**File:** `DocumentServer.csproj`

Add NuGet packages:
```xml
<!-- Office Processing -->
<PackageReference Include="ClosedXML" Version="0.105.0" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.3.0" />
<PackageReference Include="ShapeCrawler" Version="0.74.0" />
<PackageReference Include="NPOI" Version="2.7.5" />

<!-- PDF Processing -->
<PackageReference Include="itext" Version="9.3.0" />
<PackageReference Include="itext.bouncy-castle-adapter" Version="9.3.0" />
<PackageReference Include="PdfPig" Version="0.1.12-alpha-20251013-b14f4" />
<PackageReference Include="PDFsharp" Version="6.2.2" />

<!-- Search & Analysis -->
<PackageReference Include="Lucene.Net" Version="4.8.0-beta00017" />
<PackageReference Include="Lucene.Net.Analysis.Common" Version="4.8.0-beta00017" />
<PackageReference Include="Lucene.Net.QueryParser" Version="4.8.0-beta00017" />
<PackageReference Include="FuzzySharp" Version="2.0.2" />

<!-- OCR -->
<PackageReference Include="TesseractOCR" Version="5.5.1" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />

<!-- API -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.9" />
<PackageReference Include="Scalar.AspNetCore" Version="2.8.11" />
```

**Project Reference:**
```xml
<ProjectReference Include="..\MsOfficeCrypto\MsOfficeCrypto.csproj" />
```

#### Step 1.2: Extend SerializerOptions
**File:** `Common/SerializerOptions.cs`

Add new configurations as needed during implementation:
```csharp
// Example additions:
public static JsonSerializerOptions JsonOptionsCamelCase { get; } = 
    new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
public static JsonSerializerOptions JsonOptionsDefault { get; } = new();
```

#### Step 1.3: Create Core Models
**Location:** `Models/Common/`

Create the following files (one class per file):

**1. DocumentType.cs**
```csharp
namespace DocumentServer.Models.Common;

public enum DocumentType
{
    Unknown,
    Pdf,
    Word,
    Excel,
    PowerPoint,
    Image
}
```

**2. ServiceResult.cs**
```csharp
namespace DocumentServer.Models.Common;

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = new();
}
```

**3. DocumentInfo.cs**
```csharp
namespace DocumentServer.Models.Common;

public class DocumentInfo
{
    public string FilePath { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsEncrypted { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

**4. LoadedDocument.cs**
```csharp
namespace DocumentServer.Models.Common;

public class LoadedDocument
{
    public string FilePath { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public DateTime LoadedAt { get; set; }
    public object? DocumentObject { get; set; } // The actual document object
    public long MemorySizeBytes { get; set; }
}
```

#### Step 1.4: Create Request/Response Models
**Location:** `Models/Requests/` and `Models/Responses/`

Extract all request records from controllers into separate files.

**From OfficeReader:**
- `LoadDocumentRequest.cs`
- `SearchRequest.cs`
- `ExtractExcelRequest.cs`
- `ExtractPowerPointRequest.cs`
- `ExtractWordRequest.cs`
- `CompareDocumentsRequest.cs`
- `ValidateDocumentRequest.cs`

**From PdfReader:**
- `LoadPdfRequest.cs` → merge into `LoadDocumentRequest.cs`
- `SummarizeRequest.cs`
- `ExtractImagesRequest.cs`
- `ValidatePdfRequest.cs` → merge into `ValidateDocumentRequest.cs`

**From DesktopCommander DocumentTools:**
- `CreateIndexRequest.cs`
- `SearchDocumentsRequest.cs`
- `RegisterPasswordRequest.cs`
- `BulkPasswordRequest.cs`

---

### Phase 2: Core Services Implementation (Conversation 2)

**Goal:** Implement document loading, caching, and password management

#### Step 2.1: Port PasswordManager
**Source:** `DesktopCommanderMcp/Services/DocumentSearching/PasswordManager.cs`  
**Target:** `Services/Core/PasswordManager.cs`

**Actions:**
1. Copy file to target location
2. Update namespace to `DocumentServer.Services.Core`
3. Add proper logging (currently minimal in STDIO version)
4. Use `SerializerOptions` for any JSON operations
5. Add XML documentation comments for all public methods

**Key Methods to Enhance:**
```csharp
// Add comprehensive logging
public void RegisterSpecificPassword(string filePath, string password)
{
    logger.LogInformation("Registering password for document: {FilePath}", filePath);
    // ... existing logic
    logger.LogDebug("Password registered successfully for: {FilePath}", filePath);
}
```

#### Step 2.2: Create Document Cache
**Target:** `Services/Core/DocumentCache.cs`

**Source Materials:**
- `OfficeReader/Services/OfficeService.cs` (has `_documents` dictionary)
- `PdfReader/Services/PdfService.cs` (has `_documents` dictionary)

**Implementation:** Merge both caching approaches into a unified, thread-safe cache with:
- Concurrent dictionary for thread safety
- Configurable max cache size
- LRU eviction policy
- Memory pressure monitoring
- Document type abstraction

**Key Methods:**
```csharp
public interface IDocumentCache
{
    void Add(string filePath, LoadedDocument document);
    LoadedDocument? Get(string filePath);
    bool Remove(string filePath);
    void Clear();
    List<string> GetLoadedPaths();
    long GetTotalMemoryUsage();
}
```

#### Step 2.3: Create Document Loader Interfaces
**Target:** `Services/Core/IDocumentLoader.cs`

```csharp
namespace DocumentServer.Services.Core;

public interface IDocumentLoader
{
    DocumentType SupportedType { get; }
    bool CanLoad(string filePath);
    Task<ServiceResult<LoadedDocument>> LoadAsync(string filePath, string? password = null);
    Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document);
}
```

#### Step 2.4: Implement PDF Loader
**Source:** `PdfReader/Services/PdfService.cs`  
**Target:** `Services/Core/PdfDocumentLoader.cs`

**Actions:**
1. Extract loading logic from PdfService
2. Implement IDocumentLoader interface
3. Add comprehensive logging
4. Use PasswordManager for encrypted documents
5. Break into separate methods (one responsibility per method)

#### Step 2.5: Implement Office Loader
**Source:** `OfficeReader/Services/OfficeService.cs`  
**Target:** `Services/Core/OfficeDocumentLoader.cs`

**Actions:**
1. Extract loading logic from OfficeService
2. Implement IDocumentLoader interface
3. Add comprehensive logging
4. Use PasswordManager for encrypted documents
5. Handle all Office formats (Word, Excel, PowerPoint)

#### Step 2.6: Create Document Loader Factory
**Target:** `Services/Core/DocumentLoaderFactory.cs`

**Purpose:** Route document loading to appropriate loader based on file extension

```csharp
public class DocumentLoaderFactory
{
    private readonly IEnumerable<IDocumentLoader> _loaders;
    
    public DocumentLoaderFactory(IEnumerable<IDocumentLoader> loaders)
    {
        _loaders = loaders;
    }
    
    public IDocumentLoader? GetLoader(string filePath)
    {
        return _loaders.FirstOrDefault(l => l.CanLoad(filePath));
    }
}
```

---

### Phase 3: Content Extraction & Analysis (Conversation 2-3)

**Goal:** Implement unified content extraction and analysis services

#### Step 3.1: Create Content Extractor Interface
**Target:** `Services/Core/IContentExtractor.cs`

```csharp
public interface IContentExtractor
{
    DocumentType SupportedType { get; }
    Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document);
    Task<ServiceResult<Dictionary<string, string>>> ExtractMetadataAsync(LoadedDocument document);
}
```

#### Step 3.2: Implement PDF Content Extractor
**Source:** `PdfReader/Services/PdfService.cs`  
**Target:** `Services/Core/PdfContentExtractor.cs`

Port extraction methods:
- `ExtractAllTextFromDocument`
- `GetDocumentMetadata`
- `GetPageContent`

#### Step 3.3: Implement Office Content Extractor
**Source:** `OfficeReader/Services/OfficeService.cs`  
**Target:** `Services/Core/OfficeContentExtractor.cs`

Port extraction methods:
- `ExtractAllContentAsync`
- Format-specific extraction logic

#### Step 3.4: Port DocumentProcessor
**Source:** `DesktopCommanderMcp/Services/DocumentSearching/DocumentProcessor.cs`  
**Target:** `Services/Core/DocumentProcessor.cs`

**Actions:**
1. Update to use new loader/extractor interfaces
2. Add logging
3. Update namespace
4. Use SerializerOptions

#### Step 3.5: Create Analysis Services
**Location:** `Services/Analysis/`

**1. DocumentValidator.cs**
**Sources:**
- `OfficeReader/Controllers/OfficeController.cs` (ValidateDocument method)
- `PdfReader/Controllers/PdfController.cs` (ValidatePdf method)

**Merge validation logic:**
```csharp
public class DocumentValidator
{
    public async Task<ValidationResult> ValidateAsync(string filePath);
    public async Task<bool> CanOpenAsync(string filePath);
    public async Task<bool> IsCorruptedAsync(string filePath);
}
```

**2. DocumentComparator.cs**
**Sources:**
- `OfficeReader/Controllers/OfficeController.cs` (CompareDocuments method)
- `PdfReader/Controllers/PdfController.cs` (CompareDocuments method)

**3. StructureAnalyzer.cs**
**Sources:**
- `PdfReader/Services/PdfService.cs` (AnalyzeDocumentStructure method)
- `OfficeReader/Services/OfficeService.cs` (AnalyzeDocumentAsync method)

**4. MetadataExtractor.cs**
Unified metadata extraction across all formats

---

### Phase 4: Search Services (Conversation 3)

**Goal:** Implement quick search and Lucene indexing

#### Step 4.1: Create Quick Search Service
**Location:** `Services/DocumentSearch/QuickSearchService.cs`

**Sources:**
- `OfficeReader/Services/OfficeService.cs` (SearchInDocumentAsync, SearchAcrossDocumentsAsync)
- `PdfReader/Services/PdfService.cs` (SearchInDocument, SearchAcrossAllDocuments)

**Features:**
- In-memory search across loaded documents
- Fuzzy matching using FuzzySharp
- Configurable max results
- Context snippets around matches

#### Step 4.2: Port Lucene Services
**Source:** `DesktopCommanderMcp/Services/DocumentSearching/`  
**Target:** `Services/Lucene/`

**Files to port:**
1. `DocumentIndexer.cs` → `Services/Lucene/LuceneIndexer.cs`
2. Extract search logic → `Services/Lucene/LuceneSearcher.cs`
3. Create `Services/Lucene/IndexManager.cs` for lifecycle

**Actions:**
- Add comprehensive logging
- Use SerializerOptions
- Update namespaces
- Break apart if classes are too large

#### Step 4.3: Create Search Models
**Location:** `Models/Common/`

```csharp
public class SearchResult
{
    public string FilePath { get; set; }
    public DocumentType DocumentType { get; set; }
    public List<SearchMatch> Matches { get; set; }
    public float RelevanceScore { get; set; }
}

public class SearchMatch
{
    public int PageNumber { get; set; }
    public string Context { get; set; }
    public int Position { get; set; }
}
```

---

### Phase 5: OCR Services (Conversation 3)

**Goal:** Port OCR capabilities

#### Step 5.1: Port OCR Service
**Source:** `DesktopCommanderMcp/Services/DocumentSearching/OcrService.cs`  
**Target:** `Services/Ocr/OcrService.cs`

**Actions:**
1. Copy to new location
2. Add logging
3. Update namespace
4. Extract image preprocessing to separate class

#### Step 5.2: Create Supporting OCR Classes
**New Files:**
1. `Services/Ocr/TesseractEngine.cs` - Wrapper around Tesseract
2. `Services/Ocr/ImagePreprocessor.cs` - Image enhancement before OCR
3. `Services/Ocr/OcrResult.cs` - Structured OCR results

---

### Phase 6: Format-Specific Services (Conversation 4)

**Goal:** Implement format-specific operations

#### Step 6.1: Excel Services
**Source:** `OfficeReader/Services/OfficeService.cs`  
**Target:** `Services/FormatSpecific/Excel/`

**Files:**
1. `ExcelWorksheetReader.cs` - Read specific worksheets
2. `ExcelDataExtractor.cs` - Extract cell ranges, formulas

#### Step 6.2: Word Services
**Source:** `OfficeReader/Services/OfficeService.cs`  
**Target:** `Services/FormatSpecific/Word/`

**Files:**
1. `WordStructureReader.cs` - Read headings, sections
2. `WordTableExtractor.cs` - Extract tables

#### Step 6.3: PowerPoint Services
**Source:** `OfficeReader/Services/OfficeService.cs`  
**Target:** `Services/FormatSpecific/PowerPoint/`

**Files:**
1. `SlideReader.cs` - Read specific slides
2. `NotesExtractor.cs` - Extract speaker notes

#### Step 6.4: PDF Services
**Source:** `PdfReader/Services/PdfService.cs`  
**Target:** `Services/FormatSpecific/Pdf/`

**Files:**
1. `PdfPageReader.cs` - Page-level operations
2. `PdfImageExtractor.cs` - Extract images
3. `PdfSummarizer.cs` - Document summarization

---

### Phase 7: Controllers (Conversation 4-5)

**Goal:** Create unified HTTP API

#### Step 7.1: Document Controller
**Target:** `Controllers/DocumentController.cs`

**Endpoints:**
```
POST   /api/documents/load          - Load document
DELETE /api/documents/unload        - Unload document
GET    /api/documents               - List loaded documents
GET    /api/documents/info          - Get document info
POST   /api/documents/extract       - Extract content
GET    /api/documents/metadata      - Get metadata
POST   /api/documents/validate      - Validate document
POST   /api/documents/compare       - Compare documents
POST   /api/documents/analyze       - Analyze structure
DELETE /api/documents/clear         - Clear all
GET    /api/documents/status        - Service status
```

**Sources:** Merge from OfficeController and PdfController

#### Step 7.2: Search Controller
**Target:** `Controllers/SearchController.cs`

**Endpoints:**
```
POST   /api/search/quick            - Quick in-memory search
POST   /api/search/document         - Search specific document
POST   /api/search/all              - Search across all loaded
```

#### Step 7.3: Index Controller
**Target:** `Controllers/IndexController.cs`

**Endpoints:**
```
POST   /api/indexes/create          - Create Lucene index
GET    /api/indexes                 - List indexes
DELETE /api/indexes/{name}          - Delete index
POST   /api/indexes/{name}/search   - Search index
POST   /api/indexes/{name}/unload   - Unload from memory
DELETE /api/indexes/unload-all      - Unload all
GET    /api/indexes/memory          - Memory status
POST   /api/indexes/test            - Test query
```

#### Step 7.4: OCR Controller
**Target:** `Controllers/OcrController.cs`

**Endpoints:**
```
GET    /api/ocr/check               - Check if OCR needed
POST   /api/ocr/pdf                 - OCR scanned PDF
POST   /api/ocr/image               - OCR image file
GET    /api/ocr/status              - OCR service status
```

#### Step 7.5: Password Controller
**Target:** `Controllers/PasswordController.cs`

**Endpoints:**
```
POST   /api/passwords/register      - Register password
POST   /api/passwords/pattern       - Register pattern
POST   /api/passwords/bulk          - Bulk registration
POST   /api/passwords/auto-detect   - Auto-detect passwords
GET    /api/passwords               - List registered (debug)
```

#### Step 7.6: Format-Specific Controller
**Target:** `Controllers/FormatSpecificController.cs`

**Endpoints:**
```
# Excel
POST   /api/office/excel/worksheets
POST   /api/office/excel/cells

# Word
POST   /api/office/word/structure
POST   /api/office/word/tables

# PowerPoint
POST   /api/office/powerpoint/slides
POST   /api/office/powerpoint/notes

# PDF
GET    /api/pdf/pages
POST   /api/pdf/images
POST   /api/pdf/summarize
```

---

### Phase 8: Configuration & Startup (Conversation 5)

**Goal:** Configure dependency injection and startup

#### Step 8.1: Create Configuration Classes
**Location:** `Configuration/`

**Files:**
1. `DocumentServerOptions.cs` - Overall settings
2. `CacheOptions.cs` - Cache configuration
3. `LuceneOptions.cs` - Index settings
4. `OcrOptions.cs` - OCR settings

#### Step 8.2: Update Program.cs
**Target:** `Program.cs`

Register all services:
```csharp
// Core Services
builder.Services.AddSingleton<PasswordManager>();
builder.Services.AddSingleton<DocumentCache>();
builder.Services.AddSingleton<DocumentLoaderFactory>();

// Loaders
builder.Services.AddSingleton<IDocumentLoader, PdfDocumentLoader>();
builder.Services.AddSingleton<IDocumentLoader, OfficeDocumentLoader>();

// Extractors
builder.Services.AddSingleton<IContentExtractor, PdfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, OfficeContentExtractor>();
builder.Services.AddSingleton<DocumentProcessor>();

// Analysis
builder.Services.AddSingleton<DocumentValidator>();
builder.Services.AddSingleton<DocumentComparator>();
builder.Services.AddSingleton<StructureAnalyzer>();
builder.Services.AddSingleton<MetadataExtractor>();

// Search
builder.Services.AddSingleton<QuickSearchService>();
builder.Services.AddSingleton<LuceneIndexer>();
builder.Services.AddSingleton<LuceneSearcher>();
builder.Services.AddSingleton<IndexManager>();

// OCR
builder.Services.AddSingleton<OcrService>();
builder.Services.AddSingleton<TesseractEngine>();

// Format-Specific Services
// (add as needed)

// Configuration
builder.Services.Configure<DocumentServerOptions>(
    builder.Configuration.GetSection("DocumentServer"));
```

#### Step 8.3: Update appsettings.json
```json
{
  "DocumentServer": {
    "Cache": {
      "MaxSizeMB": 1024,
      "EvictionPolicy": "LRU"
    },
    "Lucene": {
      "IndexDirectory": "C:\\temp\\lucene-indexes",
      "DefaultAnalyzer": "StandardAnalyzer"
    },
    "Ocr": {
      "TesseractDataPath": "tessdata",
      "DefaultLanguage": "eng"
    }
  }
}
```

---

### Phase 9: Update DesktopCommander (Conversation 5)

**Goal:** Remove DocumentTools and update servers.json

#### Step 9.1: Remove DocumentTools
**File:** `DesktopCommanderMcp/Program.cs`

Remove line:
```csharp
.WithTools<DocumentTools>()  // DELETE THIS LINE
```

#### Step 9.2: Delete DocumentTools Files
Delete entire directory:
```
DesktopCommanderMcp/Services/DocumentSearching/
```

Delete file:
```
DesktopCommanderMcp/McpTools/DocumentTools.cs
```

#### Step 9.3: Update servers.json
**File:** `DesktopCommanderMcp/servers.json`

Add new server entry:
```json
"documents": {
  "name": "Document Server",
  "url": "https://localhost:7070",
  "port": 7070,
  "projectPath": "C:\\Users\\jorda\\RiderProjects\\McpServers\\DocumentServer",
  "startCommand": "dotnet run --launch-profile https --configuration Release",
  "requiresInit": false
}
```

#### Step 9.4: Remove Old Server Entries (Optional)
Consider deprecating:
```json
"office": { ... }   // Can be removed
"pdf": { ... }      // Can be removed
```

Or mark as deprecated:
```json
"office": {
  "name": "Office Tools (DEPRECATED - use documents)",
  ...
}
```

---

### Phase 10: Testing & Validation (Conversation 5)

**Goal:** Verify all functionality works correctly

#### Test Plan

**1. Document Loading Tests**
- Load PDF with/without password
- Load Word, Excel, PowerPoint with/without password
- Verify caching works
- Test memory limits

**2. Content Extraction Tests**
- Extract text from all formats
- Extract metadata from all formats
- Verify format-specific extraction (Excel sheets, PPT slides)

**3. Search Tests**
- Quick search in loaded documents
- Fuzzy search
- Lucene index creation
- Cross-format Lucene search
- Search with filters

**4. OCR Tests**
- Detect scanned PDFs
- Extract text from scanned PDFs
- Extract text from images
- Verify accuracy

**5. Password Tests**
- Register individual passwords
- Register password patterns
- Bulk password registration
- Auto-detection

**6. Analysis Tests**
- Document validation
- Document comparison
- Structure analysis
- Metadata extraction

**7. Format-Specific Tests**
- Excel: worksheet reading, cell extraction
- Word: structure reading, table extraction
- PowerPoint: slide reading, notes extraction
- PDF: page reading, image extraction, summarization

**8. Integration Tests**
- Load document → Extract → Search → Unload
- Create index → Search index → Delete index
- OCR → Index → Search
- Password → Load → Extract

---

## 🔄 Migration Checklist

### Pre-Migration
- [ ] Review all three source projects
- [ ] Document unique features in each
- [ ] Identify shared dependencies
- [ ] Plan testing strategy

### Phase 1 - Foundation
- [ ] Update DocumentServer.csproj with all dependencies
- [ ] Extend SerializerOptions as needed
- [ ] Create all core models (one class per file)
- [ ] Create request/response models (one class per file)

### Phase 2 - Core Services
- [ ] Port PasswordManager with logging
- [ ] Implement DocumentCache
- [ ] Create IDocumentLoader interface
- [ ] Implement PdfDocumentLoader
- [ ] Implement OfficeDocumentLoader
- [ ] Create DocumentLoaderFactory

### Phase 3 - Extraction & Analysis
- [ ] Create IContentExtractor interface
- [ ] Implement PdfContentExtractor
- [ ] Implement OfficeContentExtractor
- [ ] Port DocumentProcessor
- [ ] Create DocumentValidator
- [ ] Create DocumentComparator
- [ ] Create StructureAnalyzer
- [ ] Create MetadataExtractor

### Phase 4 - Search
- [ ] Create QuickSearchService
- [ ] Port LuceneIndexer
- [ ] Create LuceneSearcher
- [ ] Create IndexManager
- [ ] Create search models

### Phase 5 - OCR
- [ ] Port OcrService with logging
- [ ] Create TesseractEngine
- [ ] Create ImagePreprocessor
- [ ] Create OcrResult model

### Phase 6 - Format-Specific
- [ ] Excel services (2 files)
- [ ] Word services (2 files)
- [ ] PowerPoint services (2 files)
- [ ] PDF services (3 files)

### Phase 7 - Controllers
- [ ] DocumentController (main operations)
- [ ] SearchController (search operations)
- [ ] IndexController (Lucene operations)
- [ ] OcrController (OCR operations)
- [ ] PasswordController (password management)
- [ ] FormatSpecificController (format-specific ops)

### Phase 8 - Configuration
- [ ] Create configuration classes
- [ ] Update Program.cs with DI
- [ ] Update appsettings.json
- [ ] Configure logging

### Phase 9 - Update DesktopCommander
- [ ] Remove DocumentTools from Program.cs
- [ ] Delete DocumentTools.cs
- [ ] Delete DocumentSearching services
- [ ] Update servers.json
- [ ] Rebuild DesktopCommander
- [ ] Test reload_server_registry

### Phase 10 - Testing
- [ ] Document loading tests
- [ ] Content extraction tests
- [ ] Search tests
- [ ] OCR tests
- [ ] Password tests
- [ ] Analysis tests
- [ ] Format-specific tests
- [ ] Integration tests

### Post-Migration
- [ ] Update documentation
- [ ] Create API usage examples
- [ ] Performance benchmarking
- [ ] Mark old projects as deprecated
- [ ] Update README files

---

## 📚 Key Implementation Notes

### Error Handling Pattern
All service methods should return `ServiceResult<T>`:
```csharp
try
{
    logger.LogInformation("Starting operation: {Operation}", operationName);
    // ... operation logic
    logger.LogInformation("Completed operation: {Operation}", operationName);
    return new ServiceResult<T> { Success = true, Data = result };
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed operation: {Operation}", operationName);
    return new ServiceResult<T> { Success = false, Error = ex.Message };
}
```

### Logging Levels
- **Information:** Operation start/complete, major decisions
- **Debug:** Detailed flow, intermediate values
- **Warning:** Recoverable issues, fallback scenarios
- **Error:** Exceptions, operation failures

### Naming Conventions
- **Services:** `DocumentLoader`, `ContentExtractor`, `SearchService`
- **Interfaces:** `IDocumentLoader`, `IContentExtractor`
- **Models:** `LoadDocumentRequest`, `SearchResult`, `DocumentInfo`
- **Controllers:** `DocumentController`, `SearchController`

### File Organization
- One class per file (enforced)
- Related classes in same folder
- Use subfolders for organization (Excel/, Word/, etc.)
- Keep Controllers flat (no subfolders)

---

## 🎯 Success Metrics

### Token Reduction
- **Before:** ~80 tools in DesktopCommander
- **After:** ~65 tools in DesktopCommander
- **Savings:** 18-20% reduction in tool definitions

### Consolidation
- **Before:** 3 separate projects, 3 ports
- **After:** 1 unified project, 1 port
- **Reduction:** 66% fewer services to manage

### Features
- **Before:** OCR only in DocumentTools
- **After:** OCR available for all formats
- **Improvement:** 100% feature parity + enhancements

### Performance
- **Before:** Separate caches, separate processes
- **After:** Unified cache, single process
- **Improvement:** Better memory utilization

---

## 📞 Support & Questions

As we implement this plan across multiple conversations, we'll:
1. Track progress in this document
2. Update checklists as completed
3. Document any deviations from the plan
4. Note any new decisions or patterns discovered
5. Add lessons learned

**Current Status:** Planning Phase Complete ✅  
**Next Step:** Begin Phase 1 - Project Setup & Core Infrastructure