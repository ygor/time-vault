using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using TimeVault.Infrastructure.Data;
using TimeVault.Tests.Infrastructure;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TimeVault.Tests.Features.Auth
{
    [Integration]
    public class RegisterEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public RegisterEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the regular DbContext
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory database for testing
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("RegisterEndpointTests");
                    });

                    // Build the service provider
                    var sp = services.BuildServiceProvider();

                    // Create a scope to get the database context
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Ensure database is created
                    db.Database.EnsureCreated();
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Register_ShouldReturn200_WithValidRequestData()
        {
            // Arrange
            var registerRequest = new
            {
                Email = "test@example.com",
                Password = "StrongPassword!123"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/register", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);

            Assert.True(responseData.GetProperty("success").GetBoolean());
            Assert.NotEqual(string.Empty, responseData.GetProperty("token").GetString());
        }

        [Fact]
        public async Task Register_ShouldReturn400_WithWeakPassword()
        {
            // Arrange
            var registerRequest = new
            {
                Email = "test2@example.com",
                Password = "weak"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/register", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);

            // Check if 'success' property exists and has expected value
            if (PropertyExists(responseData, "success"))
            {
                Assert.False(responseData.GetProperty("success").GetBoolean());
            }
            
            // Check if 'error' property exists and contains expected text
            if (PropertyExists(responseData, "error"))
            {
                Assert.Contains("validation", responseData.GetProperty("error").GetString().ToLower());
            }
            else
            {
                // Fallback to checking the entire response string
                Assert.Contains("validation", responseString.ToLower());
            }
        }

        [Fact]
        public async Task Register_ShouldReturn400_WithInvalidEmail()
        {
            // Arrange
            var registerRequest = new
            {
                Email = "not-an-email",
                Password = "StrongPassword!123"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/register", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);

            // Check if 'success' property exists and has expected value
            if (PropertyExists(responseData, "success"))
            {
                Assert.False(responseData.GetProperty("success").GetBoolean());
            }
            
            // Check if 'error' property exists and contains expected text
            if (PropertyExists(responseData, "error"))
            {
                Assert.Contains("validation", responseData.GetProperty("error").GetString().ToLower());
            }
            else
            {
                // Fallback to checking the entire response string
                Assert.Contains("validation", responseString.ToLower());
            }
        }

        [Fact]
        public async Task Register_ShouldReturn400_WhenDuplicateEmail()
        {
            // Arrange
            var email = "duplicate@example.com";
            
            var registerRequest = new
            {
                Email = email,
                Password = "StrongPassword!123"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            // Register first user
            var firstResponse = await _client.PostAsync("/api/auth/register", content);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            // Act - attempt to register the same email
            var response = await _client.PostAsync("/api/auth/register", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(responseString);
            
            // Check for error in the response string without assuming a specific structure
            string errorText = responseString.ToLower();
            Assert.Contains("email", errorText);
            
            try
            {
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseString);
                
                // Check if 'success' property exists and has expected value
                if (PropertyExists(responseData, "success"))
                {
                    Assert.False(responseData.GetProperty("success").GetBoolean());
                }
                
                // Check if 'error' property exists and contains expected text
                if (PropertyExists(responseData, "error"))
                {
                    string errorMessage = responseData.GetProperty("error").GetString();
                    Assert.Contains("already", errorMessage.ToLower());
                }
            }
            catch (JsonException)
            {
                // If the response can't be parsed as JSON, we've already verified it contains "email"
            }
        }
        
        // Helper method to safely check if a property exists in JsonElement
        private bool PropertyExists(JsonElement element, string propertyName)
        {
            try
            {
                var property = element.GetProperty(propertyName);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
    }
} 