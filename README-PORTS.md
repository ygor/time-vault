# TimeVault Port Configuration

This document describes how to run the TimeVault application with the updated port configuration.

## Port Configuration

- Backend API: Running on port `5010`
- Frontend: Running on port `3000`

## Starting the Application

### 1. Start the Backend API

```bash
cd /Users/ygeurts/Projects/Private/time-vault/backend
./run-api-5010.sh
```

This script will start the .NET backend API on port 5010.

### 2. Start the Frontend

In a new terminal window:

```bash
cd /Users/ygeurts/Projects/Private/time-vault/frontend
npm run dev
```

This will start the Vite development server on port 3000.

### 3. Access the Application

- Frontend: http://localhost:3000
- Backend API: http://localhost:5010
- API Documentation: http://localhost:5010/swagger

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

## Port Configuration Files

The port configuration is set in the following files:

1. Backend:
   - Command line parameter: `--urls="http://localhost:5010"`
   - Used in `run-api-5010.sh`

2. Frontend:
   - `frontend/vite.config.ts` - Sets the frontend port to 3000
   - `frontend/.env` - Sets the API URL to http://localhost:5010
   - `frontend/cypress.config.cjs` - Sets the test baseUrl to http://localhost:3000 