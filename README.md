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
  - [Azure Credentials Setup](#azure-credentials-setup)
  - [Quick Deployment Steps](#quick-deployment-steps)
  - [Detailed Deployment Guides](#detailed-deployment-guides)
- [Environment Configuration](#environment-configuration)
- [Authentication](#authentication)
- [Additional Documentation](#additional-documentation)
- [License](#license)

## Key Features

- **Email-based Authentication**: Secure user authentication system based on email and password
- **Encrypted Vaults**: Create and manage time-based message vaults
- **Vault Sharing**: Share vaults with other users with specific permissions
- **Scheduled Messages**: Set precise unlock dates for messages
- **End-to-End Encryption**: Messages are encrypted before storage

## Technology Stack

- **Backend**: ASP.NET Core API with Clean Architecture
- **Database**: Azure SQL Database with Entity Framework Core
- **Authentication**: JWT-based authentication
- **Encryption**: AES-256 for data encryption
- **Infrastructure**: Azure App Service, Key Vault, Application Insights
- **CI/CD**: GitHub Actions for automated deployments
- **IaC**: Terraform for infrastructure provisioning

## Solution Structure

```
TimeVault/
├── src/
│   ├── TimeVault.Api/           # ASP.NET Core API project
│   ├── TimeVault.Core/          # Domain models and business logic
│   ├── TimeVault.Infrastructure/ # External services implementation
│   └── TimeVault.Persistence/   # Database context and repositories
├── tests/
│   ├── TimeVault.Api.Tests/     # API tests
│   ├── TimeVault.Core.Tests/    # Domain logic tests
│   └── TimeVault.Integration.Tests/ # Integration tests
└── terraform/                  # Infrastructure as Code
    ├── modules/                # Terraform modules
    └── environments/           # Environment-specific configs
```

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- SQL Server or Azure SQL Database
- Azure CLI (for deployment)
- Terraform CLI (for infrastructure provisioning)

### Local Development

1. Clone the repository
2. Set up the database connection string in `appsettings.Development.json`
3. Run the migrations:
   ```
   cd src/TimeVault.Api
   dotnet ef database update
   ```
4. Start the API:
   ```
   dotnet run
   ```

### Running Tests

```bash
dotnet test
```

## API Documentation

When running locally, API documentation is available at:
- Swagger UI: `https://localhost:5001/swagger`
- OpenAPI JSON: `https://localhost:5001/swagger/v1/swagger.json`

See [API-ENDPOINTS.md](./API-ENDPOINTS.md) for a detailed overview of all available endpoints.

## Deployment

### Azure Credentials Setup

Before deploying, you'll need to set up the following Azure credentials as GitHub Secrets:

1. **Create an Azure Service Principal**:
   ```bash
   az login
   az ad sp create-for-rbac --name "terraform-timevault" --role Contributor \
       --scopes /subscriptions/{subscription-id} --sdk-auth
   ```
   Save the JSON output from this command.

2. **Add Secrets to GitHub**:
   Navigate to your GitHub repository → Settings → Secrets and variables → Actions and add:
   
   - `AZURE_CLIENT_ID` - Service Principal client ID
   - `AZURE_CLIENT_SECRET` - Service Principal secret
   - `AZURE_SUBSCRIPTION_ID` - Your Azure subscription ID
   - `AZURE_TENANT_ID` - Your Azure tenant ID
   - `AZURE_CREDENTIALS` - The entire JSON output from service principal creation
   - `TERRAFORM_STORAGE_RG` - Resource group for Terraform state storage
   - `TERRAFORM_STORAGE_ACCOUNT` - Storage account for Terraform state
   - `SQL_ADMIN_USERNAME` - SQL Server admin username
   - `SQL_ADMIN_PASSWORD` - SQL Server admin password
   - `JWT_KEY` - Secret key for JWT token signing

### Quick Deployment Steps

1. **Backend Deployment**: 
   - Push to the appropriate branch (`develop`, `staging`, or `main`)
   - GitHub Actions will automatically deploy to the corresponding environment

2. **Frontend Deployment**:
   - Configure Azure Static Web App with GitHub integration
   - Push to your configured branch to trigger deployment

### Detailed Deployment Guides

For complete deployment instructions see:
- [AZURE-DEPLOYMENT.md](./AZURE-DEPLOYMENT.md) - Azure-specific deployment details
- [DEPLOYMENT-GUIDE.md](./DEPLOYMENT-GUIDE.md) - Comprehensive deployment guide for both backend and frontend
- [terraform/README.md](./terraform/README.md) - Terraform infrastructure documentation

## Environment Configuration

TimeVault supports multiple environments:
- **Development**: Minimal resources for local development
- **Staging**: Moderate resources for testing
- **Production**: Robust resources with redundancy for live use

## Authentication

TimeVault uses email-based authentication with JWT tokens. To authenticate:

1. Register a new user with email and password
2. Login with credentials to receive a JWT token
3. Include the token in the Authorization header for subsequent requests

## Additional Documentation

- [FRONTEND-ARCHITECTURE.md](./FRONTEND-ARCHITECTURE.md) - Frontend application architecture
- [SECURITY-GUIDE.md](./SECURITY-GUIDE.md) - Security architecture and best practices
- [FUTURE-ENHANCEMENTS.md](./FUTURE-ENHANCEMENTS.md) - Planned future features and improvements

## License

This project is licensed under the MIT License - see the LICENSE file for details. 