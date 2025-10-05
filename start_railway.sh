#!/bin/bash
# Railway startup script for Flask ML API

echo "Starting GigHub ML API on Railway..."
echo "Python version: $(python --version)"
echo "Current directory: $(pwd)"
echo "Files in directory: $(ls -la)"

# Check if model files exist
if [ -f "smart_hiring_model.pkl" ]; then
    echo "? Model file found: smart_hiring_model.pkl"
else
    echo "? Model file not found in current directory"
    echo "Searching for model file..."
    find . -name "smart_hiring_model.pkl" -type f 2>/dev/null || echo "Model file not found anywhere"
fi

if [ -f "feature_columns.json" ]; then
    echo "? Feature columns file found: feature_columns.json"
else
    echo "? Feature columns file not found in current directory"
    echo "Searching for feature columns file..."
    find . -name "feature_columns.json" -type f 2>/dev/null || echo "Feature columns file not found anywhere"
fi

# Install dependencies
echo "Installing dependencies..."
pip install -r railway_requirements.txt

# Start the application
echo "Starting Flask API..."
if [ "$FLASK_ENV" = "development" ]; then
    python standalone_flask_api.py
else
    gunicorn --bind 0.0.0.0:$PORT --workers 2 --timeout 60 --access-logfile - --error-logfile - standalone_flask_api:app
fi