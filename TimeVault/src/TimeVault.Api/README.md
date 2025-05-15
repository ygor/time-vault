# TimeVault API - Vertical Slice Architecture

This project implements vertical slice architecture, which organizes code around business features rather than by technical concerns.

## Project Structure

```
TimeVault.Api/
├── Features/                  # Organized by business features
│   ├── Auth/                  # Authentication and authorization features
│   ├── Messages/              # Message management features
│   └── Vaults/                # Vault management features
├── Infrastructure/            # Cross-cutting concerns
│   ├── Behaviors/             # MediatR pipeline behaviors
│   ├── Mapping/               # AutoMapper profiles
│   └── Middleware/            # Middleware components
└── Program.cs                 # Application configuration and startup
```

## Vertical Slice Architecture Principles

Vertical slice architecture organizes code by feature rather than by technical concerns. Each feature contains everything needed to implement a specific business capability, from the API endpoint down to the data access layer.

### Benefits

- **Isolation**: Changes to one feature have minimal impact on others.
- **Cohesion**: All code related to a feature is located in the same place.
- **Reduced cognitive load**: Developers can focus on one feature at a time.
- **Scalability**: New features can be added without affecting existing ones.

### Feature Structure

Each feature follows the Command Query Responsibility Segregation (CQRS) pattern:

- **Queries**: Used for retrieving data (read operations).
- **Commands**: Used for modifying data (create, update, delete operations).

## Implementation Details

### Feature Structure

Each feature has the following components:

- **Handler**: Processes the request and returns the response.
- **Request/Command**: Contains the input data for the operation.
- **Response**: Contains the output data for the operation.
- **Validator**: Validates the request data.

### MediatR

MediatR is used for implementing the CQRS pattern:

- **IRequest**: Marks a class as a request (command or query).
- **IRequestHandler**: Handles a specific request type.
- **IPipelineBehavior**: Implements cross-cutting concerns like validation.

### Validation

Validation is performed using FluentValidation:

- Each request has a corresponding validator.
- Validation is applied automatically using the `ValidationBehavior`.

## Adding New Features

To add a new feature:

1. Create a new file in the appropriate feature directory (e.g., `Features/Messages/CreateMessage.cs`).
2. Define the request, response, validator, and handler classes.
3. Implement the handler to process the request.

Example:

```csharp
public static class CreateMessage
{
    public class Command : IRequest<IActionResult>
    {
        public Guid VaultId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.VaultId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Content).NotEmpty();
        }
    }

    public class Handler : IRequestHandler<Command, IActionResult>
    {
        // Implementation...
    }
}
```

## Testing

The test project follows the same vertical slice architecture, organizing tests by feature rather than by technical concerns.

### Test Structure

```
TimeVault.Tests/
├── Features/                    # Tests for features
│   ├── Auth/                    # Authentication feature tests
│   ├── Messages/                # Message management feature tests
│   └── Vaults/                  # Vault management feature tests
├── Infrastructure/              # Tests for infrastructure components
│   ├── Behaviors/               # Tests for MediatR pipeline behaviors
│   └── Middleware/              # Tests for middleware components
├── Services/                    # Tests for service layer implementations
└── Integration/                 # End-to-end integration tests
```

### Test Approach

Each feature has its own test classes that verify:

1. **Command/Query handling**: Does the handler process the request correctly?
2. **Validation**: Does the validator enforce the correct rules?
3. **Business logic**: Is the business logic implemented correctly?
4. **Error handling**: Are error conditions handled appropriately?

### Testing MediatR Handlers

When testing handlers:

- Mock the dependencies (services, repositories)
- Create a test request
- Invoke the handler
- Verify the response and any side effects

Example:

```csharp
[Fact]
public async Task Handle_ShouldReturnOk_WhenMessageExists()
{
    // Arrange
    var query = new GetMessage.Query { MessageId = _messageId, UserId = _userId };
    _mockMessageService.Setup(x => x.GetMessageByIdAsync(_messageId, _userId))
        .ReturnsAsync(_testMessage);

    // Act
    var result = await _handler.Handle(query, CancellationToken.None);

    // Assert
    var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
    var returnedMessage = okResult.Value.Should().BeAssignableTo<MessageDto>().Subject;
    returnedMessage.Id.Should().Be(_messageId);
}
```

### Infrastructure Testing

Tests for infrastructure components verify that they function correctly:

- **Validation behavior**: Validates requests and throws exceptions for invalid data
- **Exception handling middleware**: Converts exceptions to appropriate HTTP responses
- **Mapping profiles**: Correctly maps between entities and DTOs

### Integration Tests

Integration tests verify that the entire system works together correctly, including:

- API endpoints
- Authentication
- Database operations
- Cross-cutting concerns 