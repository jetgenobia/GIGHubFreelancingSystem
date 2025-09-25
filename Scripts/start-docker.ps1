Write-Host "Starting GigHub Docker Environment" -ForegroundColor Green

# Check if .env file exists
if (-not (Test-Path ".env")) {
    Write-Host "ERROR: .env file not found!" -ForegroundColor Red
    Write-Host "Please copy .env.template to .env and fill in your values" -ForegroundColor Yellow
    
    if (Test-Path ".env.template") {
        Copy-Item ".env.template" ".env"
        Write-Host "Created .env file from template" -ForegroundColor Green
    }
    
    Write-Host "Please edit .env with your configuration before continuing" -ForegroundColor Yellow
    exit 1
}

# Create necessary directories
Write-Host "Creating directories..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path "Logs" | Out-Null
New-Item -ItemType Directory -Force -Path "wwwroot/uploads" | Out-Null

# Build and start services
Write-Host "Building and starting services..." -ForegroundColor Green
docker-compose up --build -d

# Wait for services to be healthy
Write-Host "Waiting for services to be ready..." -ForegroundColor Green
Start-Sleep -Seconds 10

# Check service health
Write-Host "Checking service health..." -ForegroundColor Green
docker-compose ps

Write-Host "GigHub is starting up!" -ForegroundColor Green
Write-Host "Service URLs:" -ForegroundColor Yellow
Write-Host "   Main Application: http://localhost:8080"
Write-Host "   ML API: http://localhost:5000"
Write-Host "   Database: localhost:1433"
Write-Host ""
Write-Host "To view logs:" -ForegroundColor Yellow
Write-Host "   docker-compose logs -f gighub-app"
Write-Host "   docker-compose logs -f flask-ml-api"
Write-Host ""
Write-Host "To stop:" -ForegroundColor Yellow
Write-Host "   docker-compose down"