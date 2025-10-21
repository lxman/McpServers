using System.Text.Json;
using Playwright.Core.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Register Playwright services
builder.Services.AddSingleton<PlaywrightSessionManager>();
builder.Services.AddSingleton<ToolService>();
builder.Services.AddSingleton<ChromeService>();
builder.Services.AddSingleton<FirefoxService>();
builder.Services.AddSingleton<WebKitService>();

// Add HttpClient for ApiDocumentationController
builder.Services.AddHttpClient();

// Add OpenAPI
builder.Services.AddOpenApi();

// Add CORS
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
    app.MapScalarApiReference();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();