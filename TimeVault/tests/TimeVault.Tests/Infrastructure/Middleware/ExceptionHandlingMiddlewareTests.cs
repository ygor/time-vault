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
            Assert.Equal("An error occurred while processing your request.", response.GetProperty("title").GetString());
            Assert.Equal((int)HttpStatusCode.InternalServerError, response.GetProperty("status").GetInt32());
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
            Assert.Equal("One or more validation errors occurred.", response.GetProperty("title").GetString());
            
            // Verify the error messages are included
            var errors = response.GetProperty("errors");
            
            // Verify Property1 errors
            Assert.True(errors.TryGetProperty("Property1", out JsonElement property1Errors));
            Assert.Equal(1, property1Errors.GetArrayLength());
            Assert.Equal("Error message 1", property1Errors[0].GetString());
            
            // Verify Property2 errors
            Assert.True(errors.TryGetProperty("Property2", out JsonElement property2Errors));
            Assert.Equal(1, property2Errors.GetArrayLength());
            Assert.Equal("Error message 2", property2Errors[0].GetString());
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