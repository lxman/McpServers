using OfficeReader.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Register Office services
builder.Services.AddSingleton<IDocumentDecryptionService, DocumentDecryptionService>();
builder.Services.AddSingleton<IExcelService, ExcelService>();
builder.Services.AddSingleton<IWordService, WordService>();
builder.Services.AddSingleton<IPowerPointService, PowerPointService>();
builder.Services.AddSingleton<OfficeService>();

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
