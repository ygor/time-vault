#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        Building and Publishing TimeVault API${NC}"
echo -e "${BLUE}===================================================${NC}"

# Create publish directory if it doesn't exist
mkdir -p publish

# Build and publish the application
echo -e "${BLUE}Building and publishing TimeVault API...${NC}"
dotnet publish TimeVault/src/TimeVault.Api/TimeVault.Api.csproj -c Release -o publish

# Copy the database schema script to the output directory
echo -e "${BLUE}Copying PostgreSQL schema to the publish directory...${NC}"
cp TimeVault/src/TimeVault.Infrastructure/Data/PostgresSchema.sql publish/

# Copy the Docker-related files to the root directory if they're nested
if [ -f TimeVault/Dockerfile ]; then
  cp TimeVault/Dockerfile .
fi

if [ -f TimeVault/docker-compose.yml ]; then
  cp TimeVault/docker-compose.yml .
fi

echo -e "${GREEN}Successfully published TimeVault API to the 'publish' directory${NC}"
echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}To deploy using Docker, run:${NC}"
echo -e "${BLUE}./deploy-docker.sh${NC}"
echo -e "${BLUE}===================================================${NC}" 