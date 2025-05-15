#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        Running TimeVault Integration Tests${NC}"
echo -e "${BLUE}===================================================${NC}"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo -e "${RED}Error: Docker is not running. Please start Docker first.${NC}"
  exit 1
fi

# Start PostgreSQL container for tests if not already running
CONTAINER_NAME="timevault-test-db"
if ! docker ps | grep $CONTAINER_NAME > /dev/null; then
  echo -e "${YELLOW}Starting PostgreSQL test container...${NC}"
  docker run --name $CONTAINER_NAME -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=timevault_test -p 5433:5432 -d postgres:15
  
  # Give it a moment to start up
  echo -e "${YELLOW}Waiting for PostgreSQL to start...${NC}"
  sleep 5
else
  echo -e "${YELLOW}Using existing PostgreSQL test container${NC}"
fi

# Set environment variable for test database connection
export ConnectionStrings__DefaultConnection="Host=localhost;Database=timevault_test;Username=postgres;Password=postgres;Port=5433;"

echo -e "${YELLOW}Running tests with PostgreSQL integration...${NC}"
echo -e "${BLUE}Connection string: ${GREEN}$ConnectionStrings__DefaultConnection${NC}"

# Run tests with PostgreSQL integration
cd TimeVault
dotnet test --filter "Category=Integration" || dotnet test

# Capture test exit code
TEST_EXIT=$?

# Optionally stop the container
if [ "$1" == "--cleanup" ]; then
  echo -e "${YELLOW}Stopping and removing PostgreSQL test container...${NC}"
  docker stop $CONTAINER_NAME
  docker rm $CONTAINER_NAME
else
  echo -e "${YELLOW}Leaving PostgreSQL test container running.${NC}"
  echo -e "${YELLOW}To stop it later, run: docker stop $CONTAINER_NAME && docker rm $CONTAINER_NAME${NC}"
fi

echo -e "${BLUE}===================================================${NC}"
if [ $TEST_EXIT -eq 0 ]; then
  echo -e "${GREEN}        Integration tests completed successfully!${NC}"
else
  echo -e "${RED}        Integration tests failed. See output above.${NC}"
fi
echo -e "${BLUE}===================================================${NC}"

exit $TEST_EXIT 