using AwsServer.Configuration;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add AWS services (includes response compression)
builder.Services.AddAwsServices();

// Configure JSON options
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.WriteIndented = true;
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Enable response compression for large log responses
app.UseResponseCompression();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
