#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}====================================================${NC}"
echo -e "${GREEN}        Running TimeVault API${NC}"
echo -e "${GREEN}====================================================${NC}"

# Go to the API project directory
cd TimeVault/src/TimeVault.Api

# Run the API
echo -e "${YELLOW}Starting TimeVault API...${NC}"
dotnet run

echo -e "${GREEN}====================================================${NC}"
echo -e "${GREEN}        TimeVault API stopped${NC}"
echo -e "${GREEN}====================================================${NC}" 