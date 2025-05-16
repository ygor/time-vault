using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TimeVault.Api.Infrastructure.Behaviors;
using Xunit;

namespace TimeVault.Tests.Infrastructure.Behaviors
{
    public class ValidationBehaviorTests
    {
        // Sample request class for testing
        public class TestRequest : IRequest<string> 
        {
            public string? TestProperty { get; set; }
        }

        // Sample validator for testing
        public class TestValidator : AbstractValidator<TestRequest>
        {
            public TestValidator()
            {
                RuleFor(x => x.TestProperty).NotEmpty().WithMessage("Test property cannot be empty");
            }
        }

        [Fact]
        public async Task Handle_ShouldProceedToNextDelegate_WhenValidationPasses()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new TestValidator() };
            var mockDelegate = new Mock<RequestHandlerDelegate<string>>();
            mockDelegate.Setup(d => d()).ReturnsAsync("Success");
            
            var request = new TestRequest { TestProperty = "Valid Value" };
            var mockLogger = new Mock<ILogger<ValidationBehavior<TestRequest, string>>>();
            var behavior = new ValidationBehavior<TestRequest, string>(validators, mockLogger.Object);

            // Act
            var result = await behavior.Handle(request, mockDelegate.Object, CancellationToken.None);

            // Assert
            result.Should().Be("Success");
            mockDelegate.Verify(d => d(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldThrowValidationException_WhenValidationFails()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new TestValidator() };
            var mockDelegate = new Mock<RequestHandlerDelegate<string>>();
            
            var request = new TestRequest { TestProperty = "" }; // Invalid request
            var mockLogger = new Mock<ILogger<ValidationBehavior<TestRequest, string>>>();
            var behavior = new ValidationBehavior<TestRequest, string>(validators, mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => 
                behavior.Handle(request, mockDelegate.Object, CancellationToken.None));
            
            mockDelegate.Verify(d => d(), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldValidateWithAllValidators_WhenMultipleValidatorsAreProvided()
        {
            // Arrange
            var validator1 = new Mock<IValidator<TestRequest>>();
            validator1.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            
            var validator2 = new Mock<IValidator<TestRequest>>();
            validator2.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            
            var validators = new List<IValidator<TestRequest>> { validator1.Object, validator2.Object };
            var mockDelegate = new Mock<RequestHandlerDelegate<string>>();
            mockDelegate.Setup(d => d()).ReturnsAsync("Success");
            
            var request = new TestRequest { TestProperty = "Valid Value" };
            var mockLogger = new Mock<ILogger<ValidationBehavior<TestRequest, string>>>();
            var behavior = new ValidationBehavior<TestRequest, string>(validators, mockLogger.Object);

            // Act
            await behavior.Handle(request, mockDelegate.Object, CancellationToken.None);

            // Assert
            validator1.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
            validator2.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
} 