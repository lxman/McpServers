using DocumentServer.Services.Analysis;
using DocumentServer.Services.Core;
using DocumentServer.Services.DocumentSearch;
using DocumentServer.Services.FormatSpecific.Excel;
using DocumentServer.Services.FormatSpecific.Pdf;
using DocumentServer.Services.FormatSpecific.PowerPoint;
using DocumentServer.Services.FormatSpecific.Word;
using DocumentServer.Services.Lucene;
using DocumentServer.Services.Ocr;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Core Services - Singletons for performance and caching
builder.Services.AddSingleton<PasswordManager>();
builder.Services.AddSingleton<DocumentCache>();
builder.Services.AddSingleton<DocumentDecryptionService>();
builder.Services.AddSingleton<DocumentLoaderFactory>();

// Document Loaders - Registered as implementations of IDocumentLoader
builder.Services.AddSingleton<IDocumentLoader, PdfDocumentLoader>();
builder.Services.AddSingleton<IDocumentLoader, OfficeDocumentLoader>();

// Content Extractors - Registered as implementations of IContentExtractor
builder.Services.AddSingleton<IContentExtractor, PdfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, OfficeContentExtractor>();
builder.Services.AddSingleton<DocumentProcessor>();

// Analysis Services
builder.Services.AddSingleton<DocumentValidator>();
builder.Services.AddSingleton<DocumentComparator>();
builder.Services.AddSingleton<StructureAnalyzer>();
builder.Services.AddSingleton<MetadataExtractor>();

// Search Services
builder.Services.AddSingleton<QuickSearchService>();

// Lucene Services - IndexManager must be registered before LuceneIndexer
builder.Services.AddSingleton<IndexManager>();
builder.Services.AddSingleton<LuceneIndexer>();
builder.Services.AddSingleton<LuceneSearcher>();

// OCR Services
builder.Services.AddSingleton<TesseractEngine>();

// Format-Specific Services
// Excel
builder.Services.AddSingleton<ExcelWorksheetReader>();
builder.Services.AddSingleton<ExcelDataExtractor>();

// Word
builder.Services.AddSingleton<WordStructureReader>();
builder.Services.AddSingleton<WordTableExtractor>();

// PowerPoint
builder.Services.AddSingleton<SlideReader>();
builder.Services.AddSingleton<NotesExtractor>();

// PDF
builder.Services.AddSingleton<PdfPageReader>();
builder.Services.AddSingleton<PdfImageExtractor>();
builder.Services.AddSingleton<PdfSummarizer>();

builder.Services.AddSingleton<ImagePreprocessor>();
builder.Services.AddSingleton<OcrService>();

// Configure CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Logger.LogInformation("DocumentServer starting on {Urls}", string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? ["unknown"]));

app.Run();