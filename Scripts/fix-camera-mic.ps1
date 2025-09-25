#!/usr/bin/env pwsh
# Script to fix camera/microphone issues and redeploy Docker containers

Write-Host "🔧 Fixing Camera/Microphone Issues in Docker Deployment" -ForegroundColor Green
Write-Host ""

# Step 1: Stop existing containers
Write-Host "1. Stopping existing containers..." -ForegroundColor Yellow
docker-compose down

# Step 2: Ensure HTTPS certificate exists
Write-Host "2. Ensuring HTTPS certificate exists..." -ForegroundColor Yellow
if (-not (Test-Path "https/aspnetapp.pfx")) {
    Write-Host "   Creating HTTPS certificate..." -ForegroundColor Cyan
    if (-not (Test-Path "https")) {
        New-Item -ItemType Directory -Name "https" | Out-Null
    }
    dotnet dev-certs https -ep https/aspnetapp.pfx -p GighubAdmin123! --trust
    Write-Host "   ✅ Certificate created" -ForegroundColor Green
} else {
    Write-Host "   ✅ Certificate already exists" -ForegroundColor Green
}

# Step 3: Build and start containers
Write-Host "3. Building and starting containers..." -ForegroundColor Yellow
docker-compose up --build -d

# Step 4: Wait for services to be ready
Write-Host "4. Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Step 5: Check container status
Write-Host "5. Checking container status..." -ForegroundColor Yellow
docker-compose ps

Write-Host ""
Write-Host "🎉 Deployment completed!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Service URLs:" -ForegroundColor Yellow
Write-Host "   🔒 HTTPS Application: https://localhost:8080" -ForegroundColor Cyan
Write-Host "   🌐 HTTP Application: http://localhost:8081" -ForegroundColor Cyan
Write-Host "   🤖 ML API: http://localhost:5000"
Write-Host "   🗄️  Database: localhost:1433"
Write-Host ""
Write-Host "🎥 Camera/Microphone Fix Summary:" -ForegroundColor Green
Write-Host "   ✅ Removed restrictive Permissions-Policy header"
Write-Host "   ✅ Updated Content Security Policy for media access"
Write-Host "   ✅ Configured HTTPS for secure WebRTC context"
Write-Host "   ✅ Added better error handling for camera/mic issues"
Write-Host ""
Write-Host "⚠️  Important Notes:" -ForegroundColor Yellow
Write-Host "   • Use HTTPS (https://localhost:8080) for camera/mic access"
Write-Host "   • Allow permissions when browser prompts"
Write-Host "   • Ensure no other apps are using camera/microphone"
Write-Host ""
Write-Host "📊 To view logs:" -ForegroundColor Cyan
Write-Host "   docker-compose logs -f gighub-app"
Write-Host ""
Write-Host "🛑 To stop:" -ForegroundColor Red
Write-Host "   docker-compose down"
