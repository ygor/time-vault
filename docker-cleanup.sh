#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        TimeVault Docker Cleanup Tool${NC}"
echo -e "${BLUE}===================================================${NC}"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo -e "${RED}Error: Docker is not running. Please start Docker first.${NC}"
  exit 1
fi

# Function to prompt for confirmation
function confirm {
  read -p "$1 (y/N) " -n 1 -r
  echo
  [[ $REPLY =~ ^[Yy]$ ]]
}

# Function to display usage
function show_usage {
  echo -e "Usage: $0 [OPTION]"
  echo -e "Clean up Docker resources related to TimeVault."
  echo
  echo -e "Options:"
  echo -e "  --containers    Stop and remove TimeVault containers"
  echo -e "  --volumes       Remove TimeVault volumes"
  echo -e "  --images        Remove TimeVault images"
  echo -e "  --all           Clean everything (containers, volumes, images)"
  echo -e "  --prune         Run Docker system prune (removes all unused Docker resources)"
  echo -e "  --help          Display this help message"
  echo
  echo -e "Example:"
  echo -e "  $0 --containers    # Remove only TimeVault containers"
  echo -e "  $0 --all           # Clean everything related to TimeVault"
}

# Parse command-line arguments
if [ $# -eq 0 ]; then
  show_usage
  exit 0
fi

CLEAN_CONTAINERS=false
CLEAN_VOLUMES=false
CLEAN_IMAGES=false
PRUNE_SYSTEM=false

while [ $# -gt 0 ]; do
  case "$1" in
    --containers)
      CLEAN_CONTAINERS=true
      shift
      ;;
    --volumes)
      CLEAN_VOLUMES=true
      shift
      ;;
    --images)
      CLEAN_IMAGES=true
      shift
      ;;
    --all)
      CLEAN_CONTAINERS=true
      CLEAN_VOLUMES=true
      CLEAN_IMAGES=true
      shift
      ;;
    --prune)
      PRUNE_SYSTEM=true
      shift
      ;;
    --help)
      show_usage
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}"
      show_usage
      exit 1
      ;;
  esac
done

# Stop and remove TimeVault containers
if $CLEAN_CONTAINERS; then
  echo -e "${YELLOW}Finding TimeVault containers...${NC}"
  CONTAINERS=$(docker ps -a --filter "name=timevault" --format "{{.Names}}")
  
  if [ -z "$CONTAINERS" ]; then
    echo -e "${GREEN}No TimeVault containers found.${NC}"
  else
    echo -e "${YELLOW}Found the following TimeVault containers:${NC}"
    echo "$CONTAINERS"
    
    if confirm "Do you want to stop and remove these containers?"; then
      echo -e "${YELLOW}Stopping and removing containers...${NC}"
      docker stop $CONTAINERS 2>/dev/null || true
      docker rm $CONTAINERS 2>/dev/null || true
      echo -e "${GREEN}Containers removed.${NC}"
    fi
  fi
  
  # Check for running Docker Compose services
  if [ -f "docker-compose.yml" ]; then
    echo -e "${YELLOW}Found docker-compose.yml. Checking for running services...${NC}"
    
    if docker-compose ps -q 2>/dev/null | grep -q .; then
      echo -e "${YELLOW}Found running Docker Compose services.${NC}"
      
      if confirm "Do you want to stop and remove Docker Compose services?"; then
        echo -e "${YELLOW}Stopping Docker Compose services...${NC}"
        docker-compose down
        echo -e "${GREEN}Docker Compose services stopped.${NC}"
      fi
    else
      echo -e "${GREEN}No running Docker Compose services found.${NC}"
    fi
  fi
fi

# Remove TimeVault volumes
if $CLEAN_VOLUMES; then
  echo -e "${YELLOW}Finding TimeVault volumes...${NC}"
  VOLUMES=$(docker volume ls --filter "name=timevault" --format "{{.Name}}")
  
  if [ -z "$VOLUMES" ]; then
    echo -e "${GREEN}No TimeVault volumes found.${NC}"
  else
    echo -e "${YELLOW}Found the following TimeVault volumes:${NC}"
    echo "$VOLUMES"
    
    if confirm "Do you want to remove these volumes? This will DELETE ALL DATA."; then
      echo -e "${YELLOW}Removing volumes...${NC}"
      docker volume rm $VOLUMES
      echo -e "${GREEN}Volumes removed.${NC}"
    fi
  fi
  
  # Also check for docker-compose volumes
  if [ -f "docker-compose.yml" ]; then
    COMPOSE_VOLUMES=$(docker volume ls --filter "name=time-vault" --format "{{.Name}}")
    
    if [ -n "$COMPOSE_VOLUMES" ]; then
      echo -e "${YELLOW}Found Docker Compose volumes:${NC}"
      echo "$COMPOSE_VOLUMES"
      
      if confirm "Do you want to remove these Docker Compose volumes? This will DELETE ALL DATA."; then
        echo -e "${YELLOW}Removing Docker Compose volumes...${NC}"
        docker volume rm $COMPOSE_VOLUMES
        echo -e "${GREEN}Docker Compose volumes removed.${NC}"
      fi
    fi
  fi
fi

# Remove TimeVault images
if $CLEAN_IMAGES; then
  echo -e "${YELLOW}Finding TimeVault images...${NC}"
  IMAGES=$(docker images --filter "reference=*timevault*" --format "{{.Repository}}:{{.Tag}}")
  
  if [ -z "$IMAGES" ]; then
    echo -e "${GREEN}No TimeVault images found.${NC}"
  else
    echo -e "${YELLOW}Found the following TimeVault images:${NC}"
    echo "$IMAGES"
    
    if confirm "Do you want to remove these images?"; then
      echo -e "${YELLOW}Removing images...${NC}"
      docker rmi $IMAGES
      echo -e "${GREEN}Images removed.${NC}"
    fi
  fi
fi

# Run Docker system prune if requested
if $PRUNE_SYSTEM; then
  echo -e "${YELLOW}Running Docker system prune...${NC}"
  
  if confirm "This will remove all unused Docker resources (containers, networks, volumes, images). Continue?"; then
    docker system prune --volumes -f
    echo -e "${GREEN}Docker system pruned.${NC}"
  fi
fi

echo -e "${BLUE}===================================================${NC}"
echo -e "${GREEN}        Cleanup completed!${NC}"
echo -e "${BLUE}===================================================${NC}" 