# TimeVault - Secure Time-Locked Messages

TimeVault is a secure platform for creating and sharing time-locked messages. This project consists of a .NET 7 backend API and a React/TypeScript frontend.

## Project Structure

- `/backend` - .NET 7 API with Entity Framework Core for PostgreSQL
  - `/backend/TimeVault/tests` - Backend tests including unit, integration, and schema validation tests
- `/frontend` - React + TypeScript + Vite frontend with TailwindCSS
  - `/frontend/cypress` - Frontend UI and API integration tests

## Backend Setup

1. Make sure you have .NET 7 SDK installed
2. Install PostgreSQL and create a database for the application
3. Navigate to the backend directory:

```bash
cd backend/TimeVault
```

4. Update the connection string in `appsettings.Development.json` to match your PostgreSQL settings
5. Apply database migrations:

```bash
dotnet ef database update
```

6. Start the backend API:

```bash
dotnet run --project src/TimeVault.Api
```

The API will be available at http://localhost:5000

## Frontend Setup

1. Make sure you have Node.js and NPM installed
2. Navigate to the frontend directory:

```bash
cd frontend
```

3. Install dependencies:

```bash
npm install
```

4. The `.env.development` file should already contain the API URL pointing to the backend
5. Start the development server:

```bash
npm run dev
```

The frontend will be available at http://localhost:8080

## API Integration

The frontend has been updated to integrate with the backend API:

1. Authentication uses the standard email/password flow
2. Vault creation and management are integrated with the API
3. Time-locked messages are stored and retrieved from the backend

## Running Tests

### Backend Tests

```bash
cd backend/TimeVault
dotnet test
```

### Integration Tests

```bash
cd backend
./test-integration.sh
```

### UI Tests

The project includes Cypress tests to validate the frontend integration with the backend API.

To run all UI tests:

```bash
cd frontend
npm test
```

To run specific test categories:

```bash
# Run just authentication tests
npm run test:auth

# Run vault management tests
npm run test:vaults

# Run message tests
npm run test:messages

# Run API integration tests
npm run test:api
```

To open Cypress and run tests interactively:

```bash
npm run cypress:open
```

## Test Coverage

The tests validate the following key functionality:

1. **Authentication**
   - User registration
   - Login with valid credentials
   - Rejection of invalid credentials
   - Logout functionality

2. **Vault Management**
   - Vault creation
   - Vault listing
   - Vault details

3. **Message Functionality**
   - Adding immediate messages
   - Adding future-locked messages
   - Message content visibility rules
   - Message details

4. **API Integration**
   - Authentication token usage
   - Error handling
   - Data persistence
   - UI updates after API operations

## Development Notes

- The frontend uses a proxy to direct API requests to the backend
- Authentication token is stored in localStorage
- API responses are mapped to frontend data structures in the Context providers

## Feature Roadmap

- [ ] User profile management
- [ ] Message encryption
- [ ] Advanced sharing capabilities
- [ ] Improved UX for time selection 