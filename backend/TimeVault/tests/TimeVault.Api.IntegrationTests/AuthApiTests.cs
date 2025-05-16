using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TimeVault.Api.IntegrationTests;

public class AuthApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public AuthApiTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Register_WithValidUser_ShouldReturnSuccess()
    {
        // Arrange
        var registerRequest = new
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/register", content);

        // Assert
        response.EnsureSuccessStatusCode(); // Status code 200-299
        
        var responseString = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<RegisterResponse>(
            responseString, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.User);
        Assert.Equal("test@example.com", responseData.User.Email);
        Assert.NotEmpty(responseData.Token);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange - First register a user
        var registerRequest = new
        {
            Email = "login-test@example.com",
            Password = "Password123!"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/auth/register", registerContent);

        // Arrange login request
        var loginRequest = new
        {
            Email = "login-test@example.com",
            Password = "Password123!"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", loginContent);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseString = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<LoginResponse>(
            responseString, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotEmpty(responseData.Token);
    }

    // Define response classes to deserialize API responses
    private class RegisterResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public UserData User { get; set; }
    }

    private class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public UserData User { get; set; }
    }

    private class UserData
    {
        public string Id { get; set; }
        public string Email { get; set; }
    }
} 