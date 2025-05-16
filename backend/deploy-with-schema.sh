#!/bin/bash

# Color definitions for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}Starting TimeVault API deployment with EF Core migrations...${NC}"

# Step 1: Clean and publish the .NET application
echo -e "${YELLOW}Step 1: Publishing .NET application...${NC}"
dotnet publish TimeVault/src/TimeVault.Api/TimeVault.Api.csproj -c Release -o publish

if [ $? -ne 0 ]; then
    echo -e "${RED}Failed to publish the application!${NC}"
    exit 1
fi

echo -e "${GREEN}Application published successfully${NC}"

# Step 2: Stop any running Docker containers from a previous deployment
echo -e "${YELLOW}Step 2: Stopping previous Docker containers...${NC}"
docker-compose -f docker-compose.yml down

# Step 3: Rebuild and start the Docker containers
echo -e "${YELLOW}Step 3: Starting Docker containers...${NC}"
docker-compose -f docker-compose.yml up --build

if [ $? -ne 0 ]; then
    echo -e "${RED}Failed to start Docker containers!${NC}"
    exit 1
fi

echo -e "${GREEN}Docker containers started successfully${NC}"

# Step 4: Wait for the API to start
echo -e "${YELLOW}Step 4: Waiting for API to become available...${NC}"
attempts=0
max_attempts=30
sleep_seconds=5

until $(curl --output /dev/null --silent --fail http://localhost:8081/health); do
    if [ ${attempts} -eq ${max_attempts} ]; then
        echo -e "${RED}API failed to start within the expected time frame.${NC}"
        exit 1
    fi
    
    attempts=$((attempts+1))
    remaining=$((max_attempts-attempts))
    echo -e "${YELLOW}API not ready yet. Retrying in ${sleep_seconds} seconds... (${remaining} attempts remaining)${NC}"
    sleep ${sleep_seconds}
done

echo -e "${GREEN}TimeVault API successfully deployed!${NC}"
echo -e "${BLUE}API URL: http://localhost:8081${NC}"
echo -e "${BLUE}Swagger URL: http://localhost:8081/swagger${NC}"
echo -e "${BLUE}Note: Database migrations will be automatically applied on startup${NC}"

exit 0 