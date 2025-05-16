using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Microsoft.OpenApi.Models;
using System.Reflection;
using TimeVault.Api.Infrastructure.Behaviors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TimeVault.Infrastructure.Data;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Infrastructure.Services;
using TimeVault.Api.Features.Auth.Mapping;
using TimeVault.Api.Features.Messages.Mapping;
using TimeVault.Api.Features.Vaults.Mapping;
using TimeVault.Api.Infrastructure.Middleware;
using Npgsql;
using Microsoft.Extensions.Logging;

// Create builder with minimal services
var builder = WebApplication.CreateBuilder(args);

// Add only essential services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure the database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => 
        {
            npgsqlOptions.MigrationsAssembly("TimeVault.Infrastructure");
            // Set the timestamp behavior to match traditional behavior
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        });
    
    // Configure for PostgreSQL proper case handling
    options.UseSnakeCaseNamingConvention();
});

// Register AutoMapper
builder.Services.AddAutoMapper(typeof(AuthMappingProfile), typeof(MessagesMappingProfile), typeof(VaultsMappingProfile));

// Register Core Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVaultService, VaultService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IDrandService, DrandService>();
builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();

// Register MediatR
builder.Services.AddMediatR(config => {
    config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

// Register validation pipeline behavior
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Register validators
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// HTTP clients for external services
builder.Services.AddHttpClient();

// Configure Swagger in the service configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TimeVault API",
        Version = "v1",
        Description = "A secure, time-based messaging application API"
    });
});

var app = builder.Build();

// Get logger factory
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Program");

// Log application startup details
logger.LogInformation("Starting TimeVault API in {Environment} mode", app.Environment.EnvironmentName);
logger.LogInformation("Current time: {CurrentTime}", DateTime.UtcNow);

// Add a bunch of simple endpoints for diagnostic purposes
app.MapGet("/", () => Results.Text("TimeVault API is running. Navigate to /swagger to access the API documentation.")).WithOpenApi();
app.MapGet("/health", () => Results.Json(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow
})).WithOpenApi();
app.MapGet("/environment", () => Results.Json(new { 
    env = app.Environment.EnvironmentName,
    isDevelopment = app.Environment.IsDevelopment(),
    isProduction = app.Environment.IsProduction(),
    contentRoot = app.Environment.ContentRootPath,
    webRoot = app.Environment.WebRootPath
})).WithOpenApi();
app.MapGet("/config", () => Results.Json(new { 
    urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS"),
    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
})).WithOpenApi();

// Configure Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TimeVault API v1");
});

// Use the custom exception handling middleware to properly handle validation errors
app.UseExceptionHandling();

// Basic CORS policy to ensure the API is accessible
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Configure routing and endpoints (minimal setup)
app.UseRouting();
app.MapControllers();

// Remove Username column if it exists
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Apply migrations to ensure the database schema is up to date
        try
        {
            scopedLogger.LogInformation("Applying database migrations...");
            dbContext.Database.Migrate();
            scopedLogger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            scopedLogger.LogError(ex, "Error applying database migrations: {ErrorMessage}", ex.Message);
            throw; // Re-throw the exception as this is critical
        }
        
        // Check if the Username column exists
        bool usernameColumnExists = false;
        try
        {
            // Try to query the column - if it doesn't exist, an exception will be thrown
            var test = dbContext.Database.ExecuteSqlRaw("SELECT Username FROM \"users\" LIMIT 1");
            usernameColumnExists = true;
        }
        catch
        {
            usernameColumnExists = false;
        }
        
        if (usernameColumnExists)
        {
            // Drop the unique index on Username if it exists
            try
            {
                dbContext.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS \"ix_users_username\"");
            }
            catch (Exception ex)
            {
                scopedLogger.LogWarning(ex, "Error dropping Username index: {ErrorMessage}", ex.Message);
            }
            
            // Remove the Username column
            try
            {
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE \"users\" DROP COLUMN IF EXISTS \"username\"");
                scopedLogger.LogInformation("Username column removed successfully");
            }
            catch (Exception ex)
            {
                scopedLogger.LogWarning(ex, "Error removing Username column: {ErrorMessage}", ex.Message);
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during database schema modification: {ErrorMessage}", ex.Message);
}

logger.LogInformation("Application startup complete - endpoints are ready");
app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
