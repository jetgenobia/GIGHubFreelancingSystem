from flask import Flask, request, jsonify
import joblib
import pandas as pd
import numpy as np
from flask_cors import CORS

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# Initialize model variable
model = None

# Load the model
try:
    model = joblib.load('smart_hiring_model.pkl')
    print("Random Forest model loaded successfully")
    print(f"Model type: {type(model)}")
    print(f"Model is not None: {model is not None}")
except Exception as e:
    print(f"Error loading model: {e}")
    model = None

@app.route('/predict', methods=['POST'])
def predict():
    try:
        if model is None:
            return jsonify({'error': 'Model not loaded'}), 500
        
        # Get features from request
        data = request.json
        features = data.get('features', {})
        
        # Define expected feature columns (now includes mentorship completion)
        feature_columns = [
            "skill_match_score", "avg_rating", "recommendation_rate", "completion_rate",
            "bid_success_rate", "category_experience", "response_time_hours", 
            "portfolio_quality", "budget_match_score", "delivery_time_days",
            "freelancer_tenure_days", "project_complexity", "client_history_score",
            "past_collaboration", "skills_count_match", "workload_factor",
            "mentorship_program_completed"  # New feature added
        ]
        
        # Create feature array in correct order
        feature_array = []
        for col in feature_columns:
            feature_array.append(float(features.get(col, 0.0)))
        
        # Reshape for prediction
        X = np.array([feature_array])
        
        # Make prediction
        prediction = model.predict_proba(X)
        probability = float(prediction[0][1])
        
        return jsonify({
            'success': True,
            'prediction': probability,
            'message': 'Random Forest prediction successful',
            'features_used': len(feature_columns)
        })
        
    except Exception as e:
        return jsonify({
            'error': f'Prediction failed: {str(e)}'
        }), 500

@app.route('/health', methods=['GET'])
def health():
    print(f"Health check - model is not None: {model is not None}")
    print(f"Health check - model type: {type(model)}")
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None,
        'features_count': 17,
        'debug_model_type': str(type(model)) if model is not None else 'None'
    })

if __name__ == '__main__':
    print("Starting Flask API for Random Forest...")
    app.run(host='0.0.0.0', port=5000, debug=False)