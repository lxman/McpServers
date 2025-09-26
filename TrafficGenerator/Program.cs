using Microsoft.OpenApi.Models;
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

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { 
                Title = "Traffic Generator API", 
                Version = "v1",
                Description = "REST API for generating network traffic for penetration testing scenarios"
            });
            
            // Include XML comments if available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

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
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Traffic Generator API v1");
                c.RoutePrefix = "swagger"; // Explicit route prefix
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
        Console.WriteLine("Swagger UI available at: http://localhost:5000/swagger");
        Console.WriteLine("Health check available at: http://localhost:5000/health");

        app.Run();
    }
}
