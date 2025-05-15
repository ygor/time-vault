#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Database connection parameters - defaults
DB_HOST=${DB_HOST:-"localhost"}
DB_PORT=${DB_PORT:-"5432"}
DB_NAME=${DB_NAME:-"timevault"}
DB_USER=${DB_USER:-"postgres"}
DB_PASSWORD=${DB_PASSWORD:-"postgres"}

# Function to display usage information
function show_usage {
  echo -e "${BLUE}TimeVault PostgreSQL Database Maintenance Script${NC}"
  echo
  echo "Usage: $0 [OPTIONS] COMMAND"
  echo
  echo "Commands:"
  echo "  init        Initialize database with schema"
  echo "  backup      Create a database backup"
  echo "  restore     Restore from a backup file"
  echo "  reset       Reset database (drop and recreate)"
  echo "  status      Check database connection and status"
  echo
  echo "Options:"
  echo "  --host      Database host (default: localhost)"
  echo "  --port      Database port (default: 5432)"
  echo "  --dbname    Database name (default: timevault)"
  echo "  --user      Database username (default: postgres)"
  echo "  --password  Database password (default: postgres)"
  echo "  --file      Backup file for restore operation"
  echo "  --help      Show this help message"
  echo
  echo "Environment variables:"
  echo "  DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD can be used instead of options"
  echo
  echo "Examples:"
  echo "  $0 init"
  echo "  $0 backup"
  echo "  $0 --host=postgres --port=5432 status"
  echo "  $0 restore --file=backup.sql"
  echo
}

# Parse command line arguments
COMMAND=""
BACKUP_FILE=""

while [[ $# -gt 0 ]]; do
  case $1 in
    init|backup|restore|reset|status)
      COMMAND=$1
      shift
      ;;
    --host=*)
      DB_HOST="${1#*=}"
      shift
      ;;
    --port=*)
      DB_PORT="${1#*=}"
      shift
      ;;
    --dbname=*)
      DB_NAME="${1#*=}"
      shift
      ;;
    --user=*)
      DB_USER="${1#*=}"
      shift
      ;;
    --password=*)
      DB_PASSWORD="${1#*=}"
      shift
      ;;
    --file=*)
      BACKUP_FILE="${1#*=}"
      shift
      ;;
    --help)
      show_usage
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}" >&2
      show_usage
      exit 1
      ;;
  esac
done

if [ -z "$COMMAND" ]; then
  echo -e "${RED}Error: No command specified${NC}"
  show_usage
  exit 1
fi

# Connection string for psql commands
export PGPASSWORD=$DB_PASSWORD
PSQL_CONN="psql -h $DB_HOST -p $DB_PORT -U $DB_USER"
PSQL_CONN_DB="$PSQL_CONN -d $DB_NAME"

# Check if PostgreSQL is accessible
function check_postgres {
  echo -e "${BLUE}Checking PostgreSQL connection...${NC}"
  if $PSQL_CONN -c "SELECT version();" > /dev/null 2>&1; then
    echo -e "${GREEN}PostgreSQL connection successful!${NC}"
    return 0
  else
    echo -e "${RED}Failed to connect to PostgreSQL at $DB_HOST:$DB_PORT${NC}"
    return 1
  fi
}

# Check if database exists
function check_database {
  if $PSQL_CONN -lqt | cut -d \| -f 1 | grep -qw $DB_NAME; then
    echo -e "${GREEN}Database '$DB_NAME' exists${NC}"
    return 0
  else
    echo -e "${YELLOW}Database '$DB_NAME' does not exist${NC}"
    return 1
  fi
}

# Initialize database with schema
function init_database {
  echo -e "${BLUE}Initializing database with schema...${NC}"
  
  # Check for schema file
  SCHEMA_FILE="TimeVault/src/TimeVault.Infrastructure/Data/PostgresSchema.sql"
  if [ ! -f "$SCHEMA_FILE" ]; then
    echo -e "${RED}Schema file not found: $SCHEMA_FILE${NC}"
    exit 1
  fi
  
  # Create database if it doesn't exist
  if ! check_database; then
    echo -e "${YELLOW}Creating database '$DB_NAME'...${NC}"
    $PSQL_CONN -c "CREATE DATABASE $DB_NAME;" || exit 1
  fi
  
  # Apply schema
  echo -e "${BLUE}Applying schema to database...${NC}"
  $PSQL_CONN_DB -f "$SCHEMA_FILE" || exit 1
  
  echo -e "${GREEN}Database initialization completed successfully${NC}"
}

# Create a database backup
function backup_database {
  if ! check_database; then
    echo -e "${RED}Cannot backup non-existent database${NC}"
    exit 1
  fi
  
  TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
  BACKUP_FILENAME="${DB_NAME}_${TIMESTAMP}.sql"
  
  echo -e "${BLUE}Creating backup of database '$DB_NAME' to '$BACKUP_FILENAME'...${NC}"
  pg_dump -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f "$BACKUP_FILENAME" || exit 1
  
  echo -e "${GREEN}Backup completed successfully: $BACKUP_FILENAME${NC}"
}

# Restore database from backup
function restore_database {
  if [ -z "$BACKUP_FILE" ]; then
    echo -e "${RED}Error: No backup file specified. Use --file=backup.sql${NC}"
    exit 1
  fi
  
  if [ ! -f "$BACKUP_FILE" ]; then
    echo -e "${RED}Backup file not found: $BACKUP_FILE${NC}"
    exit 1
  fi
  
  # Create database if it doesn't exist
  if ! check_database; then
    echo -e "${YELLOW}Creating database '$DB_NAME' for restore...${NC}"
    $PSQL_CONN -c "CREATE DATABASE $DB_NAME;" || exit 1
  fi
  
  echo -e "${BLUE}Restoring database from '$BACKUP_FILE'...${NC}"
  $PSQL_CONN_DB -f "$BACKUP_FILE" || exit 1
  
  echo -e "${GREEN}Database restore completed successfully${NC}"
}

# Reset database (drop and recreate)
function reset_database {
  echo -e "${RED}WARNING: This will DELETE ALL DATA in database '$DB_NAME'${NC}"
  read -p "Are you sure you want to continue? (y/N) " -n 1 -r
  echo
  
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    if check_database; then
      echo -e "${YELLOW}Dropping database '$DB_NAME'...${NC}"
      $PSQL_CONN -c "DROP DATABASE $DB_NAME;" || exit 1
    fi
    
    echo -e "${YELLOW}Creating fresh database '$DB_NAME'...${NC}"
    $PSQL_CONN -c "CREATE DATABASE $DB_NAME;" || exit 1
    
    # Apply schema
    SCHEMA_FILE="TimeVault/src/TimeVault.Infrastructure/Data/PostgresSchema.sql"
    if [ -f "$SCHEMA_FILE" ]; then
      echo -e "${BLUE}Applying schema to database...${NC}"
      $PSQL_CONN_DB -f "$SCHEMA_FILE" || exit 1
    fi
    
    echo -e "${GREEN}Database reset completed successfully${NC}"
  else
    echo -e "${YELLOW}Database reset cancelled${NC}"
  fi
}

# Check database status
function show_status {
  echo -e "${BLUE}Database Connection Details:${NC}"
  echo -e "  Host:     ${GREEN}$DB_HOST${NC}"
  echo -e "  Port:     ${GREEN}$DB_PORT${NC}"
  echo -e "  Database: ${GREEN}$DB_NAME${NC}"
  echo -e "  User:     ${GREEN}$DB_USER${NC}"
  
  if ! check_postgres; then
    exit 1
  fi
  
  if check_database; then
    echo -e "${BLUE}Database Details:${NC}"
    
    # Get table counts
    echo -e "${BLUE}Table Statistics:${NC}"
    $PSQL_CONN_DB -c "
      SELECT 
        table_name, 
        (SELECT count(*) FROM \"$DB_NAME\".public.\""'$1'"\") as row_count 
      FROM information_schema.tables 
      WHERE table_schema = 'public'
      ORDER BY table_name;
    " || exit 1
    
    # Database size
    echo -e "${BLUE}Database Size:${NC}"
    $PSQL_CONN_DB -c "
      SELECT 
        pg_size_pretty(pg_database_size('$DB_NAME')) as database_size;
    " || exit 1
  fi
}

# Execute requested command
case $COMMAND in
  init)
    check_postgres
    init_database
    ;;
  backup)
    check_postgres
    backup_database
    ;;
  restore)
    check_postgres
    restore_database
    ;;
  reset)
    check_postgres
    reset_database
    ;;
  status)
    show_status
    ;;
esac 