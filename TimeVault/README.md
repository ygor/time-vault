# TimeVault

TimeVault is a secure time-locked message vault application built with ASP.NET Core. It allows users to create encrypted messages that can only be accessed after a specific time has passed.

## Features

- Robust user authentication with JWT tokens
- Create and manage vaults to organize your messages
- Share vaults with other users (with read-only or edit permissions)
- Create time-locked messages that unlock at a specified date and time
- Secure message encryption for sensitive content
- RESTful API with Swagger documentation

## Technology Stack

- **Backend:** ASP.NET Core 8.0
- **Database:** SQL Server with Entity Framework Core
- **Authentication:** JWT token-based authentication
- **API Documentation:** Swagger / OpenAPI
- **Encryption:** AES for message content encryption

## Project Structure

The solution follows a Clean Architecture pattern:

- **TimeVault.Api**: API controllers and presentation layer
- **TimeVault.Core**: Application business logic and interfaces
- **TimeVault.Domain**: Domain entities and business models
- **TimeVault.Infrastructure**: Data access and external service implementations

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- SQL Server (or SQL Server LocalDB for development)
- Visual Studio 2022, VS Code, or Rider

### Setup

1. Clone the repository:
```bash
git clone https://github.com/yourusername/TimeVault.git
cd TimeVault
```

2. Set up the database:
   - Update the connection string in `appsettings.json` if needed
   - Run the Entity Framework migrations:
```bash
cd src/TimeVault.Api
dotnet ef database update
```

3. Run the application:
```bash
dotnet run
```

The API will be available at https://localhost:5001 and the Swagger documentation at https://localhost:5001/swagger.

### Default Credentials

A default admin user is created when the application starts:

- Username: admin
- Email: admin@timevault.com
- Password: Admin123!

**Important**: Change these credentials in production!

## API Endpoints

The TimeVault API provides the following main endpoint groups:

- **Auth**: `/api/auth` - Authentication endpoints for login, registration, etc.
- **Vaults**: `/api/vaults` - Endpoints for managing vaults and vault sharing
- **Messages**: `/api/messages` - Endpoints for creating and accessing time-locked messages

For complete API documentation, refer to the Swagger UI.

## How Time-Locking Works

TimeVault uses symmetric encryption (AES) to encrypt message content. When a user creates a message with an unlock time:

1. The content is encrypted using AES with a random key
2. The encrypted content is stored in the database
3. When the unlock time passes, the message can be decrypted and displayed to authorized users

## Security Considerations

- All user passwords are securely hashed
- JWT tokens are used for authentication
- Message content is encrypted at rest
- API requires HTTPS
- Authorization checks prevent unauthorized access to vaults and messages

## License

This project is licensed under the MIT License - see the LICENSE file for details. 