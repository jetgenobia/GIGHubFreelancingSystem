# GigHub ML Service Deployment Guide

This guide explains how to deploy the Random Forest ML model for Smart Hiring in Railway.

## Current Issue

The system is using fallback scoring instead of the Random Forest model because the Flask API service is not running in Railway.

## Solution: Deploy Flask API as Separate Railway Service

### Step 1: Create New Railway Service for Flask API

1. **Create a new Railway project** for the Flask ML API
2. **Connect to the same GitHub repository** but configure it to run the Flask service

### Step 2: Configure Flask API Service in Railway

#### Files to Deploy:
- `standalone_flask_api.py` (main Flask application)
- `railway_requirements.txt` (Python dependencies)
- `wwwroot/models/smart_hiring_model.pkl` (trained model)
- `wwwroot/models/feature_columns.json` (feature configuration)

#### Railway Configuration:

**Build Command:**
```bash
pip install -r railway_requirements.txt
```

**Start Command:**
```bash
gunicorn --bind 0.0.0.0:$PORT --workers 2 --timeout 60 standalone_flask_api:app
```

**Environment Variables:**
```
FLASK_ENV=production
PORT=$PORT
```

### Step 3: Update Main Application

In your main Railway service (ASP.NET Core app), add this environment variable:

```
FLASK_API_URL=https://your-flask-service-url.railway.app
```

Replace `your-flask-service-url` with the actual URL of your deployed Flask service.

### Step 4: Verify Deployment

1. Check Flask API health: `https://your-flask-service-url.railway.app/health`
2. Use the ML Diagnostics page in your admin panel: `/MLDiagnostics`
3. Test the connection and verify the Random Forest service is available

## Alternative: Single Service Deployment

If you prefer to run everything in one Railway service:

1. **Remove the Railway environment check** in `LocalRandomForestService.cs`
2. **Modify the Dockerfile** to include Python and run both services
3. **Update the startup script** to run both ASP.NET Core and Flask

## Testing the Setup

### Local Testing
```bash
# Start Flask API
python standalone_flask_api.py

# Test health endpoint
curl http://localhost:5000/health

# Test prediction
curl -X POST http://localhost:5000/predict \
  -H "Content-Type: application/json" \
  -d '{"features": {"skill_match_score": 0.8, "avg_rating": 4.5, ...}}'
```

### Railway Testing
1. Visit your Flask service URL: `https://your-flask-service.railway.app/health`
2. Check the ML Diagnostics page in your main app
3. Monitor Railway logs for both services

## File Structure

```
??? standalone_flask_api.py          # Main Flask application
??? railway_requirements.txt         # Python dependencies
??? railway.toml                     # Railway configuration
??? start_railway.sh                 # Startup script
??? wwwroot/
?   ??? models/
?       ??? smart_hiring_model.pkl   # Trained model
?       ??? feature_columns.json     # Feature configuration
?       ??? flask_api.py             # Original Flask API (local)
??? Controllers/
?   ??? MLDiagnosticsController.cs   # Diagnostics controller
??? Views/
    ??? MLDiagnostics/
        ??? Index.cshtml              # Diagnostics page
```

## Troubleshooting

### Common Issues:

1. **Model file not found**
   - Ensure `smart_hiring_model.pkl` is in the root directory of Flask service
   - Check Railway build logs for file copying issues

2. **Flask service not starting**
   - Check Railway logs for Python/dependency errors
   - Verify `railway_requirements.txt` has correct dependencies

3. **Connection timeout**
   - Increase timeout in `LocalRandomForestService.cs`
   - Check network connectivity between services

4. **Environment variables**
   - Ensure `FLASK_API_URL` is set in main service
   - Use Railway's environment variable management

### Debug Commands:

```bash
# Check if model exists
ls -la *.pkl

# Test Flask API directly
python standalone_flask_api.py

# Check Python dependencies
pip list | grep -E "(flask|scikit|joblib|pandas|numpy)"
```

## Performance Notes

- Flask API uses 2 Gunicorn workers for better concurrency
- Predictions are cached in the main application
- Model is loaded once at startup
- Consider increasing Railway resources for ML workloads

## Security Notes

- Flask API includes CORS headers for cross-origin requests
- Admin-only access to diagnostics page
- No sensitive data logged in predictions
- Timeout protection for ML requests