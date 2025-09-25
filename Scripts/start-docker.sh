#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🚀 Starting GigHub Docker Environment${NC}"

# Check if .env file exists
if [ ! -f .env ]; then
    echo -e "${RED}❌ .env file not found!${NC}"
    echo -e "${YELLOW}📝 Please copy .env.template to .env and fill in your values${NC}"
    cp .env.template .env
    echo -e "${GREEN}✅ Created .env file from template${NC}"
    echo -e "${YELLOW}⚠️  Please edit .env with your configuration before continuing${NC}"
    exit 1
fi

# Create necessary directories
echo -e "${GREEN}📁 Creating directories...${NC}"
mkdir -p Logs
mkdir -p wwwroot/uploads

# Build and start services
echo -e "${GREEN}🔨 Building and starting services...${NC}"
docker-compose up --build -d

# Wait for services to be healthy
echo -e "${GREEN}⏳ Waiting for services to be ready...${NC}"
sleep 10

# Check service health
echo -e "${GREEN}🏥 Checking service health...${NC}"
docker-compose ps

echo -e "${GREEN}✅ GigHub is starting up!${NC}"
echo -e "${YELLOW}📋 Service URLs:${NC}"
echo -e "   🌐 Main Application: http://localhost:8080"
echo -e "   🤖 ML API: http://localhost:5000"
echo -e "   🗄️  Database: localhost:1433"
echo -e ""
echo -e "${YELLOW}📊 To view logs:${NC}"
echo -e "   docker-compose logs -f gighub-app"
echo -e "   docker-compose logs -f flask-ml-api"
echo -e ""
echo -e "${YELLOW}🛑 To stop:${NC}"
echo -e "   docker-compose down"
