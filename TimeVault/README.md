# TimeVault API

TimeVault is a secure, time-based messaging application that allows users to create vaults and share encrypted messages that can only be accessed at specific dates and times.

## Table of Contents

- [Key Features](#key-features)
- [Technology Stack](#technology-stack)
- [Solution Structure](#solution-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Local Development](#local-development)
  - [Running Tests](#running-tests)
- [API Documentation](#api-documentation)
- [Deployment](#deployment)
  - [Docker Deployment](#docker-deployment)
  - [Azure Deployment](#azure-deployment)
- [Environment Configuration](#environment-configuration)
- [Authentication](#authentication)
- [How Time-Locking Works](#how-time-locking-works)
- [Security Considerations](#security-considerations)
- [License](#license)

## Key Features

- **Email-based Authentication**: Secure user authentication system based on email and password
- **Encrypted Vaults**: Create and manage time-based message vaults
- **Vault Sharing**: Share vaults with other users with specific permissions
- **Scheduled Messages**: Set precise unlock dates for messages
- **End-to-End Encryption**: Messages are encrypted before storage
- **Time-Locked Encryption**: Uses decentralized randomness (Drand) for time-lock encryption

## Technology Stack

- **Backend**: ASP.NET Core 7.0 API with Clean Architecture and vertical slice architecture
- **Database**: PostgreSQL with Entity Framework Core
- **Authentication**: JWT-based authentication
- **Encryption**: AES-256 for data encryption, Identity-Based Encryption for time-locking
- **Containerization**: Docker & Docker Compose for deployment
- **API Documentation**: Swagger/OpenAPI

## Solution Structure

```
TimeVault/
├── src/
│   ├── TimeVault.Api/           # ASP.NET Core API project with vertical slice features
│   ├── TimeVault.Core/          # Domain interfaces and core services
│   ├── TimeVault.Domain/        # Domain models and entities
│   └── TimeVault.Infrastructure/ # External services implementation
├── tests/
│   └── TimeVault.Tests/         # Unit and integration tests
```

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- PostgreSQL or Docker with Docker Compose
- Git

### Local Development

1. Clone the repository
2. Configure the database connection string in `appsettings.Development.json`
3. Run the API:
   ```
   ./run-api.sh
   ```
   Or manually:
   ```
   cd src/TimeVault.Api
   dotnet run
   ```

### Running Tests

Run all tests:
```bash
./run-tests.sh
```

Run integration tests with a PostgreSQL Docker container:
```bash
./test-integration.sh
```

## API Documentation

When running locally, API documentation is available at:
- Swagger UI: `http://localhost:8080/swagger`
- OpenAPI JSON: `http://localhost:8080/swagger/v1/swagger.json`

## Deployment

### Docker Deployment

1. Build and publish the application:
   ```bash
   ./publish.sh
   ```

2. Deploy with Docker Compose:
   ```bash
   ./deploy-docker.sh
   ```

   The API will be available at http://localhost:8080 and Swagger at http://localhost:8080/swagger.

### Azure Deployment

For Azure deployment, see the appropriate documentation in the repository.

## Environment Configuration

TimeVault supports configuration through environment variables and appsettings.json:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=timevault;Username=postgres;Password=postgres;Port=5432;"
  },
  "Jwt": {
    "Key": "your_secure_key_here",
    "Issuer": "TimeVault.API",
    "Audience": "TimeVault.Client",
    "ExpiryInDays": 7
  }
}
```

## Authentication

TimeVault uses email-based authentication with JWT tokens. To authenticate:

1. Register a new user with email and password
2. Login with credentials to receive a JWT token
3. Include the token in the Authorization header for subsequent requests

## How Time-Locking Works

TimeVault uses two encryption methods:

1. **Symmetric Encryption (AES)** for regular private messages
2. **Identity-Based Encryption (IBE) with Drand** for time-locked messages:
   - Messages are encrypted using the Drand public key and a future round number
   - The decryption key becomes available only when the round is reached
   - Messages are further encrypted with a vault-specific key for additional security

## Security Considerations

- All user passwords are securely hashed with HMACSHA512
- JWT tokens are used for authentication with proper expiration
- Message content is encrypted using industry-standard algorithms
- Vault-specific encryption ensures messages can only be decrypted by authorized users
- Time-locked encryption ensures messages cannot be decrypted before their unlock time

## License

This project is licensed under the MIT License - see the LICENSE file for details. 