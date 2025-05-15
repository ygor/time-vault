#!/bin/bash

# Terminal colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        Starting TimeVault API for Development${NC}"
echo -e "${BLUE}===================================================${NC}"

# Navigate to the API project directory
cd TimeVault/src/TimeVault.Api

# Check if the database exists and create if it doesn't
echo -e "${BLUE}Ensuring database migrations are applied...${NC}"
dotnet ef database update

# Run the API with development settings
echo -e "${BLUE}Starting API server...${NC}"
echo -e "${BLUE}The API will be available at ${GREEN}https://localhost:5001${NC}"
echo -e "${BLUE}Swagger documentation: ${GREEN}https://localhost:5001/swagger${NC}"
echo -e "${BLUE}Default credentials: admin / Admin123!${NC}"
echo -e "${BLUE}===================================================${NC}"

# Run the API with hot reload
dotnet watch run --urls="https://localhost:5001" 