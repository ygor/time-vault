using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TimeVault.Infrastructure.Data;
using TimeVault.Api.Infrastructure.Middleware;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Infrastructure.Services;
using TimeVault.Api.Features.Auth.Mapping;
using TimeVault.Api.Features.Messages.Mapping;
using TimeVault.Api.Features.Vaults.Mapping;
using System.Reflection;

namespace TimeVault.Tests.Infrastructure
{
    /// <summary>
    /// Test startup class for configuring the application in test mode
    /// </summary>
    public class TestStartup
    {
        public IConfiguration Configuration { get; }

        public TestStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Basic services
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            // In-memory database for unit tests
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TimeVaultTestDb"));

            // AutoMapper profiles
            services.AddAutoMapper(
                typeof(AuthMappingProfile),
                typeof(MessagesMappingProfile),
                typeof(VaultsMappingProfile));

            // Register services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IVaultService, VaultService>();
            services.AddScoped<IMessageService, MessageService>();
            services.AddScoped<IDrandService, DrandService>();
            services.AddScoped<IKeyVaultService, KeyVaultService>();

            // Register MediatR
            services.AddMediatR(config => {
                config.RegisterServicesFromAssemblies(
                    typeof(TimeVault.Api.Features.Auth.AuthController).Assembly);
            });

            // HTTP clients for external services
            services.AddHttpClient();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Use exception handling middleware
            app.UseExceptionHandling();

            // Configure basic middleware
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
} 