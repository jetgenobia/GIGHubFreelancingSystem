from flask import Flask, request, jsonify
import joblib
import pandas as pd
import numpy as np
from flask_cors import CORS

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# Load the model
try:
    model = joblib.load('smart_hiring_model.pkl')
    print("Random Forest model loaded successfully")
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
        
        # Define expected feature columns
        feature_columns = [
            "skill_match_score", "avg_rating", "recommendation_rate", "completion_rate",
            "bid_success_rate", "category_experience", "response_time_hours", 
            "portfolio_quality", "budget_match_score", "delivery_time_days",
            "freelancer_tenure_days", "project_complexity", "client_history_score",
            "past_collaboration", "skills_count_match", "workload_factor"
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
            'message': 'Random Forest prediction successful'
        })
        
    except Exception as e:
        return jsonify({
            'error': f'Prediction failed: {str(e)}'
        }), 500

@app.route('/health', methods=['GET'])
def health():
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None
    })

if __name__ == '__main__':
    print("Starting Flask API for Random Forest...")
    app.run(host='0.0.0.0', port=5000, debug=False)
