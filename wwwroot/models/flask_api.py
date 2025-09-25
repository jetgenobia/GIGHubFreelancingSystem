from flask import Flask, request, jsonify
import joblib
import pandas as pd
import numpy as np
from flask_cors import CORS
import os
import sys
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# Load the model
model = None
try:
    model_path = os.path.join(os.path.dirname(__file__), 'smart_hiring_model.pkl')
    if os.path.exists(model_path):
        model = joblib.load(model_path)
        logger.info("Random Forest model loaded successfully")
    else:
        logger.error(f"Model file not found at {model_path}")
except Exception as e:
    logger.error(f"Error loading model: {e}")
    model = None

@app.route('/predict', methods=['POST'])
def predict():
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
        
        # Define expected feature columns
        feature_columns = [
            "skill_match_score", "avg_rating", "recommendation_rate", "completion_rate",
            "bid_success_rate", "category_experience", "response_time_hours", 
            "portfolio_quality", "budget_match_score", "delivery_time_days",
            "freelancer_tenure_days", "project_complexity", "client_history_score",
            "past_collaboration", "skills_count_match", "workload_factor",
            "mentorship_program_completed"
        ]
        
        # Create feature array in correct order
        feature_array = []
        missing_features = []
        
        for col in feature_columns:
            if col in features:
                try:
                    feature_array.append(float(features[col]))
                except (ValueError, TypeError):
                    feature_array.append(0.0)
                    logger.warning(f"Invalid value for feature {col}, using 0.0")
            else:
                feature_array.append(0.0)
                missing_features.append(col)
        
        if missing_features:
            logger.info(f"Missing features (using 0.0): {missing_features}")
        
        # Reshape for prediction
        X = np.array([feature_array])
        
        # Make prediction
        prediction = model.predict_proba(X)
        probability = float(prediction[0][1])
        
        return jsonify({
            'success': True,
            'prediction': probability,
            'message': 'Random Forest prediction successful',
            'features_used': len(feature_columns),
            'missing_features': missing_features if missing_features else None
        })
        
    except Exception as e:
        logger.error(f"Prediction error: {str(e)}")
        return jsonify({
            'success': False,
            'error': f'Prediction failed: {str(e)}'
        }), 500

@app.route('/health', methods=['GET'])
def health():
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None,
        'features_count': 17,
        'service': 'GigHub ML API'
    })

@app.route('/', methods=['GET'])
def root():
    return jsonify({
        'message': 'GigHub ML API is running',
        'endpoints': ['/health', '/predict'],
        'model_status': 'loaded' if model is not None else 'not_loaded'
    })

if __name__ == '__main__':
    logger.info("Starting Flask API for Random Forest...")
    logger.info(f"Python version: {sys.version}")
    logger.info(f"Working directory: {os.getcwd()}")
    logger.info(f"Model loaded: {model is not None}")
    
    # Use Gunicorn in production, Flask dev server for development
    if os.getenv('FLASK_ENV') == 'development':
        app.run(host='0.0.0.0', port=5000, debug=True)
    else:
        app.run(host='0.0.0.0', port=5000, debug=False)