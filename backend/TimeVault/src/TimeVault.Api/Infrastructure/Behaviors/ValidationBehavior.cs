using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TimeVault.Api.Infrastructure.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> 
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

        public ValidationBehavior(
            IEnumerable<IValidator<TRequest>> validators,
            ILogger<ValidationBehavior<TRequest, TResponse>> logger)
        {
            _validators = validators;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request, 
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
            {
                _logger.LogDebug("No validators found for request of type {RequestType}", typeof(TRequest).Name);
                return await next();
            }

            _logger.LogDebug("Validating request of type {RequestType}", typeof(TRequest).Name);
            var context = new ValidationContext<TRequest>(request);
            
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            
            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Any())
            {
                _logger.LogWarning("Validation failed for request of type {RequestType} with {ErrorCount} errors", 
                    typeof(TRequest).Name, failures.Count);
                    
                foreach (var error in failures)
                {
                    _logger.LogDebug("Validation error: {PropertyName}: {ErrorMessage}", 
                        error.PropertyName, error.ErrorMessage);
                }
                
                throw new ValidationException(failures);
            }

            _logger.LogDebug("Validation successful for request of type {RequestType}", typeof(TRequest).Name);
            return await next();
        }
    }
} 