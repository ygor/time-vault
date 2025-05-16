#!/bin/bash

# Terminal colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        Running TimeVault Tests${NC}"
echo -e "${BLUE}===================================================${NC}"

# Navigate to the root project directory
cd TimeVault

# Check if a specific test filter was provided
if [ -z "$1" ]; then
  echo -e "${BLUE}Running all tests...${NC}"
  dotnet test
else
  echo -e "${BLUE}Running tests matching filter: ${GREEN}$1${NC}"
  dotnet test --filter "FullyQualifiedName~$1"
fi

# Check the exit status
if [ $? -eq 0 ]; then
  echo -e "${BLUE}===================================================${NC}"
  echo -e "${GREEN}        All tests passed successfully!${NC}"
  echo -e "${BLUE}===================================================${NC}"
else
  echo -e "${BLUE}===================================================${NC}"
  echo -e "${RED}        Tests failed. Please check the output above.${NC}"
  echo -e "${BLUE}===================================================${NC}"
fi 