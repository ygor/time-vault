#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}====================================================${NC}"
echo -e "${GREEN}        Deploying TimeVault API with Docker${NC}"
echo -e "${GREEN}====================================================${NC}"

# First publish the application
echo -e "${YELLOW}Publishing application...${NC}"
./publish.sh

# Build and start Docker containers
echo -e "${YELLOW}Building and starting Docker containers...${NC}"
docker-compose down
docker-compose build
docker-compose up -d

echo -e "${GREEN}====================================================${NC}"
echo -e "${GREEN}        TimeVault API deployed successfully${NC}"
echo -e "${GREEN}====================================================${NC}"
echo -e "${YELLOW}API URL:${NC} http://localhost:5000"
echo -e "${YELLOW}Swagger URL:${NC} http://localhost:5000/swagger" 