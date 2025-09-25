#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${RED}🛑 Stopping GigHub Docker Environment${NC}"

# Stop and remove containers
docker-compose down

echo -e "${GREEN}✅ GigHub services stopped${NC}"
echo -e "${YELLOW}💾 Database data is preserved in Docker volume${NC}"
echo -e "${YELLOW}🗂️  To remove all data: docker-compose down -v${NC}"
