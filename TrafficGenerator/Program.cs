using Scalar.AspNetCore;
using TrafficGenerator.Services;

namespace TrafficGenerator;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddAuthorization();

        // Register our traffic generation service
        builder.Services.AddScoped<ITrafficGenerationService, TrafficGenerationService>();

        // Add OpenAPI document generation
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        // Add CORS for development
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
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
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Traffic Generator API")
                       .WithTheme(ScalarTheme.Purple)
                       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
            app.UseCors("AllowAll");
        }

        // Remove HTTPS redirection in the development environment
        // app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        // Add a health check endpoint
        app.MapGet("/health", () => new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        }).WithName("HealthCheck")
          .WithSummary("Health Check")
          .WithDescription("Returns the health status of the Traffic Generator API");;

        Console.WriteLine("Traffic Generator API starting...");
        Console.WriteLine("Health check available at: http://localhost:5000/health");
        Console.WriteLine("API Documentation (Scalar) available at: http://localhost:5000/scalar/v1");

        app.Run();
    }
}
