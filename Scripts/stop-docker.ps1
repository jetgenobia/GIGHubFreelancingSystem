# PowerShell version for Windows
Write-Host "Stopping GigHub Docker Environment" -ForegroundColor Red

# Stop and remove containers
docker-compose down

Write-Host "GigHub services stopped" -ForegroundColor Green
Write-Host "Database data is preserved in Docker volume" -ForegroundColor Yellow
Write-Host "To remove all data: docker-compose down -v" -ForegroundColor Yellow