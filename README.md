# TimeVault

TimeVault is a secure, time-locked message vault application that uses drand's distributed randomness beacon for threshold encryption.

## Engineering Principles

The TimeVault project follows these engineering principles:

### 1. Test Organization and Structure
- Tests are organized by functionality and follow a consistent pattern
- Each test class focuses on testing a specific component or integration point
- Test methods are named descriptively using the `ClassUnderTest_MethodUnderTest_ExpectedBehavior` pattern
- XML documentation comments explain the purpose of each test

### 2. Mock Behavior and Setup
- Mocks use `MockBehavior.Strict` to enforce explicit setup of all method calls
- Mock setup is organized into dedicated private helper methods for maintainability
- Reusable test constants are defined at the class level for consistency
- Test data setup is isolated in dedicated helper methods

### 3. Test Isolation and Reproducibility
- Each test uses a unique database instance to prevent cross-test interference
- Tests use helper methods to create test data consistently
- Tests avoid using real external dependencies by mocking interfaces
- Database contexts use `EnableSensitiveDataLogging()` for better diagnostics

### 4. Assertions and Verification
- Tests use fluent assertions for readable verification logic
- Tests verify both state and behavior when appropriate
- Tests focus on valuable assertions that validate correctness
- Complex test scenarios are broken down into manageable steps

## Features

- Secure message storage with time-lock encryption
- Multi-user access control with fine-grained permissions
- Two-layer encryption using both time-lock and vault-specific keys
- Automatic message unlocking when time conditions are met

## Technology Stack

- **Backend:** ASP.NET Core 8.0
- **Database:** SQL Server with Entity Framework Core
- **Authentication:** JWT token-based authentication
- **API Documentation:** Swagger / OpenAPI
- **Encryption:** AES for message content encryption
- **Architecture:** Vertical Slice Architecture with CQRS pattern
- **Testing:** xUnit, FluentAssertions, Moq

## Project Structure

The solution follows a Vertical Slice Architecture pattern:

- **TimeVault.Api**: API controllers, features (commands/queries), and infrastructure
- **TimeVault.Core**: Core interfaces and services
- **TimeVault.Domain**: Domain entities and business models
- **TimeVault.Infrastructure**: Data access and service implementations

### Vertical Slice Architecture

The TimeVault API has been refactored to use Vertical Slice Architecture, which organizes code around features rather than technical concerns. Each feature contains its own:

- Controller endpoint
- Command/Query handlers (using MediatR)
- Validation logic (using FluentValidation)
- DTOs and mapping

Benefits of this architecture include:
- Better separation of concerns
- Improved maintainability
- Feature isolation
- Easier testability

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- SQL Server or SQL Server Express (for local development)

### Running the Application

1. Clone the repository
2. Navigate to the project directory
3. Run `dotnet restore` to restore dependencies
4. Run `dotnet build` to build the solution
5. Run `dotnet run --project TimeVault/src/TimeVault.Api` to start the API

### Running Tests

```
dotnet test TimeVault/tests/TimeVault.Tests
```

## Project Structure

- **TimeVault.Domain**: Contains domain entities and business rules
- **TimeVault.Core**: Contains interfaces, DTOs, and service abstractions
- **TimeVault.Infrastructure**: Contains service implementations and data access
- **TimeVault.Api**: API controllers and configuration
- **TimeVault.Tests**: Unit and integration tests

## API Endpoints

The TimeVault API provides the following main endpoint groups:

- **Auth**: `/api/auth`