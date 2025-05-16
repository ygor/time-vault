#!/bin/bash

# Pre-deployment checklist script for TimeVault
# This script verifies that the application is ready for deployment
# by running a series of checks against a local test environment.

echo "=== TimeVault Pre-Deployment Checklist ==="
echo "Running checks to verify application readiness..."
echo ""

# Store the current directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# Navigate to the project directory
cd "$PROJECT_DIR"

# Check 1: Verify code builds without errors
echo "Check 1: Building code..."
dotnet build
if [ $? -ne 0 ]; then
    echo "❌ Build failed. Fix build errors before deploying."
    exit 1
else
    echo "✅ Build successful."
fi
echo ""

# Check 2: Run unit tests
echo "Check 2: Running unit tests..."
dotnet test --filter Category!=Integration
if [ $? -ne 0 ]; then
    echo "❌ Unit tests failed. Fix failing tests before deploying."
    exit 1
else
    echo "✅ Unit tests passed."
fi
echo ""

# Check 3: Verify database migrations apply on a clean database
echo "Check 3: Verifying database migrations..."
# Create a temporary test database
TEST_DB_NAME="timevault_predeployment_$(date +%s)"
echo "Creating test database $TEST_DB_NAME..."

# Create test database
psql -h localhost -U postgres -c "CREATE DATABASE $TEST_DB_NAME;" 2>/dev/null
if [ $? -ne 0 ]; then
    echo "❌ Failed to create test database. Make sure PostgreSQL is running."
    exit 1
fi

# Apply migrations
cd src/TimeVault.Api
export ConnectionStrings__DefaultConnection="Host=localhost;Database=$TEST_DB_NAME;Username=postgres;Password=postgres"
dotnet ef database update --project ../TimeVault.Infrastructure
if [ $? -ne 0 ]; then
    echo "❌ Failed to apply migrations. Verify all migrations are valid."
    # Clean up
    psql -h localhost -U postgres -c "DROP DATABASE $TEST_DB_NAME;" 2>/dev/null
    exit 1
else
    echo "✅ Migrations applied successfully."
fi
cd "$PROJECT_DIR"

# Clean up test database
psql -h localhost -U postgres -c "DROP DATABASE $TEST_DB_NAME;" 2>/dev/null
echo ""

# Check 4: Run integration tests
echo "Check 4: Running integration tests..."
dotnet test --filter Category=Integration
if [ $? -ne 0 ]; then
    echo "❌ Integration tests failed. Fix failing tests before deploying."
    exit 1
else
    echo "✅ Integration tests passed."
fi
echo ""

# Check 5: Test API endpoints
echo "Check 5: Testing API endpoints..."
echo "Starting API in background..."
cd src/TimeVault.Api
dotnet run --no-build &
API_PID=$!

# Wait for API to start
echo "Waiting for API to start..."
sleep 5

# Test health endpoint
echo "Testing health endpoint..."
HEALTH_RESPONSE=$(curl -s http://localhost:5000/health)
if [[ $HEALTH_RESPONSE == *"healthy"* ]]; then
    echo "✅ Health endpoint check passed."
else
    echo "❌ Health endpoint check failed."
    kill $API_PID
    exit 1
fi

# Test registration endpoint with invalid data (should return 400)
echo "Testing registration validation..."
REG_RESPONSE=$(curl -s -X POST -H "Content-Type: application/json" -d '{"email":"test", "password":"short"}' http://localhost:5000/api/auth/register -w "%{http_code}" -o /dev/null)
if [ "$REG_RESPONSE" -eq 400 ]; then
    echo "✅ Registration validation check passed."
else
    echo "❌ Registration validation check failed."
    kill $API_PID
    exit 1
fi

# Stop the API
kill $API_PID
cd "$PROJECT_DIR"
echo ""

# Success - all checks passed
echo "=== All pre-deployment checks passed! ==="
echo "The application is ready for deployment."
exit 0 