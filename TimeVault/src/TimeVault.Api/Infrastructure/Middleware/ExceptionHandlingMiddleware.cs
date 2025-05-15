using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using TimeVault.Api.Infrastructure.Common;

namespace TimeVault.Api.Infrastructure.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during request processing");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var response = exception switch
            {
                ValidationException validationEx => HandleValidationException(validationEx, context),
                UnauthorizedAccessException _ => HandleUnauthorizedAccessException(context),
                // Add more specific exception types as needed
                _ => HandleUnknownException(exception, context)
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonSerializerPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);
            
            await context.Response.WriteAsync(json);
        }

        private static Result HandleValidationException(ValidationException exception, HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            
            var errors = new List<string>();
            foreach (var error in exception.Errors)
            {
                errors.Add(error.ErrorMessage);
            }
            
            return Result.ValidationFailed(errors);
        }

        private static Result HandleUnauthorizedAccessException(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return Result.Failure("You are not authorized to access this resource");
        }

        private static Result HandleUnknownException(Exception exception, HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return Result.Failure("An unexpected error occurred");
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }

    // Helper class to ensure JSON property names are camelCase
    public class JsonSerializerPolicy : JsonNamingPolicy
    {
        public static new JsonNamingPolicy CamelCase => new JsonSerializerPolicy();

        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
} 