using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using TimeVault.Api;
using Xunit;

namespace TimeVault.Api.IntegrationTests
{
    public class BasicRouteTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public BasicRouteTests(CustomWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task RootEndpoint_ShouldReturn200()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AuthRegisterEndpoint_ShouldBeAvailable()
        {
            // Arrange
            var registerRequest = new
            {
                Email = "test-basic@example.com",
                Password = "Password123!"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/register", content);

            // Assert
            // We just want to verify the endpoint exists, not its behavior
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
        
        [Fact]
        public async Task AuthLoginEndpoint_ShouldBeAvailable()
        {
            // Arrange
            var loginRequest = new
            {
                Email = "test-basic@example.com",
                Password = "Password123!"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/login", content);

            // Assert
            // We just want to verify the endpoint exists, not its behavior
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
        
        [Fact]
        public async Task VaultsEndpoint_ShouldExist()
        {
            // Act
            var response = await _client.GetAsync("/api/vaults");

            // Assert
            // Should be Unauthorized, not NotFound, if the endpoint exists
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
} 