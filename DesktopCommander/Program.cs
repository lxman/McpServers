using DesktopCommander.Services;
using DesktopCommander.Services.AdvancedFileEditing;
using DesktopCommander.Services.DocumentSearching;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Register DesktopCommander services
builder.Services.AddSingleton<SecurityManager>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<FileVersionService>();
builder.Services.AddSingleton<ProcessManager>();
builder.Services.AddSingleton<HexAnalysisService>();

// File editing services
builder.Services.AddSingleton<EditApprovalService>();
builder.Services.AddSingleton<FileEditor>();
builder.Services.AddSingleton<LineBasedEditor>();
builder.Services.AddSingleton<DiffPatchService>();
builder.Services.AddSingleton<IndentationManager>();

// Document search services
builder.Services.AddSingleton<DocumentIndexer>();
builder.Services.AddSingleton<DocumentProcessor>();
builder.Services.AddSingleton<OcrService>();
builder.Services.AddSingleton<PasswordManager>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();
