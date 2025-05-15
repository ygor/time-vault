using FluentValidation;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

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
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var response = new ErrorResponse
            {
                Title = "An error occurred while processing your request.",
                Status = (int)statusCode
            };

            if (exception is ValidationException validationException)
            {
                statusCode = HttpStatusCode.BadRequest;
                response.Title = "One or more validation errors occurred.";
                response.Status = (int)statusCode;
                response.Errors = new Dictionary<string, List<string>>();

                foreach (var error in validationException.Errors)
                {
                    var propertyName = error.PropertyName;
                    var errorMessage = error.ErrorMessage;

                    if (!response.Errors.ContainsKey(propertyName))
                    {
                        response.Errors[propertyName] = new List<string>();
                    }

                    response.Errors[propertyName].Add(errorMessage);
                }
            }
            else
            {
                _logger.LogError(exception, "An unhandled exception occurred.");
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private class ErrorResponse
        {
            public string Title { get; set; }
            public int Status { get; set; }
            public Dictionary<string, List<string>> Errors { get; set; }
        }
    }

    // Extension method to add the middleware to the HTTP request pipeline
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
} 