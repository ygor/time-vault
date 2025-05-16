# TimeVault Port Configuration

This document describes how to run the TimeVault application with the updated port configuration.

## Port Configuration

- Backend API: Running on port `8081`
- Frontend: Running on port `3001`

## Starting the Application

### 1. Start the Backend API

```bash
cd /Users/ygeurts/Projects/Private/time-vault/backend
./run-api-8081.sh
```

This script will start the .NET backend API on port 8081.

### 2. Start the Frontend

In a new terminal window:

```bash
cd /Users/ygeurts/Projects/Private/time-vault/frontend
npm run dev
```

This will start the Vite development server on port 3001.

### 3. Access the Application

- Frontend: http://localhost:3001
- Backend API: http://localhost:8081
- API Documentation: http://localhost:8081/swagger

## Running Tests

### Backend Tests

```bash
cd /Users/ygeurts/Projects/Private/time-vault/backend/TimeVault
dotnet test
```

### Frontend Tests (Cypress)

Make sure both the backend and frontend are running before executing the tests:

```bash
cd /Users/ygeurts/Projects/Private/time-vault/frontend
npm test
```

## Docker Deployment

If you're using Docker to deploy the application:

```bash
cd /Users/ygeurts/Projects/Private/time-vault/backend
./deploy-with-schema.sh
```

This will build and deploy the application with the correct port configuration.

## Port Configuration Files

The port configuration is set in the following files:

1. Backend:
   - Command line parameter: `--urls="http://localhost:8081"`
   - Used in `run-api-8081.sh`
   - Docker Compose: Maps container port 80 to host port 8081

2. Frontend:
   - `frontend/vite.config.ts` - Sets the frontend port to 3001
   - API proxy configuration in `vite.config.ts` points to the backend at http://localhost:8081
   - `frontend/cypress.config.cjs` - Cypress tests configured to use http://localhost:3001 