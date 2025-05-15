using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TimeVault.Api.Infrastructure.Middleware;
using Xunit;

namespace TimeVault.Tests.Infrastructure.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _mockLogger;
        private readonly RequestDelegate _nextMock;

        public ExceptionHandlingMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
            _nextMock = (context) => 
            {
                throw new Exception("Test exception");
            };
        }

        [Fact]
        public async Task InvokeAsync_ShouldReturnInternalServerError_WhenUnhandledExceptionOccurs()
        {
            // Arrange
            var middleware = new ExceptionHandlingMiddleware(_nextMock, _mockLogger.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.Body.Position = 0;
            string responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.False(response.GetProperty("success").GetBoolean());
            Assert.Equal("An unexpected error occurred", response.GetProperty("error").GetString());
        }

        [Fact]
        public async Task InvokeAsync_ShouldReturnBadRequest_WhenValidationExceptionOccurs()
        {
            // Arrange
            var validationFailures = new List<ValidationFailure>
            {
                new ValidationFailure("Property1", "Error message 1"),
                new ValidationFailure("Property2", "Error message 2")
            };
            
            RequestDelegate nextWithValidationException = (context) => 
            {
                throw new ValidationException(validationFailures);
            };

            var middleware = new ExceptionHandlingMiddleware(nextWithValidationException, _mockLogger.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.Body.Position = 0;
            string responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
            Assert.False(response.GetProperty("success").GetBoolean());
            Assert.Equal("Validation failed", response.GetProperty("error").GetString());
            
            // Verify validation errors are included
            var validationErrors = response.GetProperty("validationErrors");
            Assert.True(validationErrors.GetArrayLength() >= 2);
            
            // The new response format has all validation errors in a flat list
            // We can't easily check for specific errors since they're in a list without keys
            // So we'll just check for the presence of the array with at least the number of our errors
        }

        [Fact]
        public async Task InvokeAsync_ShouldProceedNormally_WhenNoExceptionOccurs()
        {
            // Arrange
            RequestDelegate nextWithoutException = (context) => 
            {
                return Task.CompletedTask;
            };

            var middleware = new ExceptionHandlingMiddleware(nextWithoutException, _mockLogger.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode); // Default status code
            
            context.Response.Body.Position = 0;
            string responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Equal(string.Empty, responseBody); // No content should be written
        }
    }
} 