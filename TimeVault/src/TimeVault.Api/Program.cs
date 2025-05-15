using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using TimeVault.Api.Infrastructure.Behaviors;
using TimeVault.Api.Features.Auth.Mapping;
using TimeVault.Api.Features.Messages.Mapping;
using TimeVault.Api.Features.Vaults.Mapping;
using TimeVault.Api.Infrastructure.Middleware;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Infrastructure.Data;
using TimeVault.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add MediatR for vertical slice architecture
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    // Register validation behavior
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Add AutoMapper with feature-specific profiles
builder.Services.AddAutoMapper(cfg =>
{
    // Register feature-specific mapping profiles
    cfg.AddProfile<AuthMappingProfile>();
    cfg.AddProfile<MessagesMappingProfile>();
    cfg.AddProfile<VaultsMappingProfile>();
});

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVaultService, VaultService>();
builder.Services.AddScoped<IDrandService, DrandService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// Add HttpClient services
builder.Services.AddHttpClient("DrandClient", client =>
{
    client.BaseAddress = new Uri("https://api.drand.sh");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TimeVault API",
        Version = "v1",
        Description = "API for TimeVault - a time-locked message vault with secure storage and retrieval",
    });

    // Configure Swagger to use JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            builder.WithOrigins(
                "http://localhost:3000",
                "https://timevault.app")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandling(); // Custom exception handling middleware
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create a default admin user on startup if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();

        // Seed default admin user
        var authService = services.GetRequiredService<IAuthService>();
        await SeedData.InitializeAsync(authService);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }

public static class SeedData
{
    public static async Task InitializeAsync(IAuthService authService)
    {
        // Create admin user if it doesn't exist
        var (success, _, _, error) = await authService.RegisterAsync(
            "admin@timevault.com",
            "Admin123!");

        // We don't care if this fails because the user already exists
    }
}
