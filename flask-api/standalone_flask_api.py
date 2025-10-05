#!/usr/bin/env python3
"""
Standalone Flask API for GigHub Smart Hiring ML Model
Optimized for Railway deployment
"""

from flask import Flask, request, jsonify
import joblib
import pandas as pd
import numpy as np
from flask_cors import CORS
import os
import sys
import logging
import json
from pathlib import Path

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# Global variables
model = None
feature_columns = []

def load_model_and_features():
    """Load the ML model and feature configuration"""
    global model, feature_columns
    
    try:
        # Try different possible model locations
        model_paths = [
            'smart_hiring_model.pkl',
            'wwwroot/models/smart_hiring_model.pkl',
            './wwwroot/models/smart_hiring_model.pkl',
            os.path.join(os.path.dirname(__file__), 'smart_hiring_model.pkl'),
            os.path.join(os.path.dirname(__file__), 'wwwroot', 'models', 'smart_hiring_model.pkl')
        ]
        
        model_loaded = False
        for model_path in model_paths:
            if os.path.exists(model_path):
                logger.info(f"Attempting to load model from: {model_path}")
                model = joblib.load(model_path)
                model_loaded = True
                logger.info(f"Random Forest model loaded successfully from {model_path}")
                break
        
        if not model_loaded:
            logger.error(f"Model file not found in any of these locations: {model_paths}")
            return False
        
        # Load feature columns
        feature_paths = [
            'feature_columns.json',
            'wwwroot/models/feature_columns.json',
            './wwwroot/models/feature_columns.json',
            os.path.join(os.path.dirname(__file__), 'feature_columns.json'),
            os.path.join(os.path.dirname(__file__), 'wwwroot', 'models', 'feature_columns.json')
        ]
        
        features_loaded = False
        for feature_path in feature_paths:
            if os.path.exists(feature_path):
                logger.info(f"Loading feature columns from: {feature_path}")
                with open(feature_path, 'r') as f:
                    feature_columns = json.load(f)
                features_loaded = True
                logger.info(f"Feature columns loaded: {len(feature_columns)} features")
                break
        
        if not features_loaded:
            # Use default feature columns if file not found
            feature_columns = [
                "skill_match_score", "avg_rating", "recommendation_rate", "completion_rate",
                "bid_success_rate", "category_experience", "response_time_hours", 
                "portfolio_quality", "budget_match_score", "delivery_time_days",
                "freelancer_tenure_days", "project_complexity", "client_history_score",
                "past_collaboration", "skills_count_match", "workload_factor",
                "mentorship_program_completed"
            ]
            logger.warning(f"Feature columns file not found, using defaults: {len(feature_columns)} features")
        
        return True
        
    except Exception as e:
        logger.error(f"Error loading model or features: {e}")
        return False

@app.route('/predict', methods=['POST'])
def predict():
    """Make ML prediction using the Random Forest model"""
    try:
        if model is None:
            return jsonify({
                'success': False,
                'error': 'Model not loaded',
                'message': 'Random Forest model is not available'
            }), 500
        
        # Get features from request
        data = request.json
        if not data:
            return jsonify({
                'success': False,
                'error': 'No data provided'
            }), 400
            
        features = data.get('features', {})
        logger.info(f"Received prediction request with {len(features)} features")
        
        # Create feature array in correct order
        feature_array = []
        missing_features = []
        invalid_features = []
        
        for col in feature_columns:
            if col in features:
                try:
                    value = float(features[col])
                    # Basic validation
                    if np.isnan(value) or np.isinf(value):
                        feature_array.append(0.0)
                        invalid_features.append(col)
                    else:
                        feature_array.append(value)
                except (ValueError, TypeError) as e:
                    feature_array.append(0.0)
                    invalid_features.append(col)
                    logger.warning(f"Invalid value for feature {col}: {features[col]} - {e}")
            else:
                feature_array.append(0.0)
                missing_features.append(col)
        
        # Log feature processing info
        if missing_features:
            logger.info(f"Missing features (using 0.0): {missing_features}")
        if invalid_features:
            logger.warning(f"Invalid features (using 0.0): {invalid_features}")
        
        # Reshape for prediction
        X = np.array([feature_array])
        logger.info(f"Feature array shape: {X.shape}")
        
        # Make prediction
        prediction_proba = model.predict_proba(X)
        prediction = model.predict(X)
        
        # Get probability of positive class (assuming binary classification)
        if prediction_proba.shape[1] > 1:
            probability = float(prediction_proba[0][1])  # Probability of positive class
        else:
            probability = float(prediction_proba[0][0])
        
        # Ensure probability is between 0 and 1
        probability = max(0.0, min(1.0, probability))
        
        logger.info(f"Prediction successful: {probability:.3f}")
        
        return jsonify({
            'success': True,
            'prediction': probability,
            'predicted_class': int(prediction[0]),
            'message': 'Random Forest prediction successful',
            'model_info': {
                'features_used': len(feature_columns),
                'missing_features': missing_features if missing_features else None,
                'invalid_features': invalid_features if invalid_features else None,
                'feature_array_length': len(feature_array)
            }
        })
        
    except Exception as e:
        logger.error(f"Prediction error: {str(e)}", exc_info=True)
        return jsonify({
            'success': False,
            'error': f'Prediction failed: {str(e)}',
            'type': type(e).__name__
        }), 500

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    model_info = {}
    if model is not None:
        try:
            # Get basic model information
            if hasattr(model, 'n_estimators'):
                model_info['n_estimators'] = model.n_estimators
            if hasattr(model, 'n_features_in_'):
                model_info['n_features_in'] = model.n_features_in_
        except Exception as e:
            logger.warning(f"Could not get model info: {e}")
    
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None,
        'features_count': len(feature_columns),
        'service': 'GigHub ML API',
        'model_info': model_info,
        'python_version': sys.version,
        'environment': os.getenv('FLASK_ENV', 'production')
    })

@app.route('/', methods=['GET'])
def root():
    """Root endpoint with API information"""
    return jsonify({
        'message': 'GigHub ML API is running',
        'version': '1.0.0',
        'endpoints': {
            '/': 'API information',
            '/health': 'Health check',
            '/predict': 'Make ML predictions (POST)'
        },
        'model_status': 'loaded' if model is not None else 'not_loaded',
        'features_count': len(feature_columns)
    })

@app.errorhandler(404)
def not_found(error):
    return jsonify({
        'error': 'Endpoint not found',
        'message': 'The requested endpoint does not exist',
        'available_endpoints': ['/', '/health', '/predict']
    }), 404

@app.errorhandler(405)
def method_not_allowed(error):
    return jsonify({
        'error': 'Method not allowed',
        'message': 'The HTTP method is not allowed for this endpoint'
    }), 405

@app.errorhandler(500)
def internal_error(error):
    logger.error(f"Internal server error: {error}")
    return jsonify({
        'error': 'Internal server error',
        'message': 'An unexpected error occurred'
    }), 500

def main():
    """Main function to run the Flask app"""
    logger.info("Starting GigHub ML API...")
    logger.info(f"Python version: {sys.version}")
    logger.info(f"Working directory: {os.getcwd()}")
    logger.info(f"Files in current directory: {os.listdir('.')}")
    
    # Load model and features
    if load_model_and_features():
        logger.info("Model and features loaded successfully")
    else:
        logger.error("Failed to load model or features")
    
    # Get configuration from environment
    host = os.getenv('HOST', '0.0.0.0')
    port = int(os.getenv('PORT', 5000))
    debug = os.getenv('FLASK_ENV') == 'development'
    
    logger.info(f"Starting server on {host}:{port}")
    logger.info(f"Debug mode: {debug}")
    logger.info(f"Model loaded: {model is not None}")
    
    # Run the app
    app.run(host=host, port=port, debug=debug)

if __name__ == '__main__':
    main()