# Complete workflow: Export data, train model, test prediction
import requests
import subprocess
import sys
import os

def export_training_data():
    """Export training data from your application"""
    try:
        # Call your application's export endpoint
        response = requests.get('http://localhost:5000/api/MLTraining/export-data')
        
        if response.status_code == 200:
            with open('training_data.csv', 'wb') as f:
                f.write(response.content)
            print("✓ Training data exported successfully")
            return True
        else:
            print(f"❌ Failed to export data: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Error exporting data: {e}")
        return False

def train_model():
    """Train the model using the local training script"""
    try:
        result = subprocess.run([
            sys.executable, 'train_smart_hiring_model_local.py', 'training_data.csv'
        ], capture_output=True, text=True)
        
        print("Training output:")
        print(result.stdout)
        
        if result.stderr:
            print("Training errors:")
            print(result.stderr)
        
        return result.returncode == 0
    except Exception as e:
        print(f"❌ Error training model: {e}")
        return False

def test_prediction():
    """Test the trained model"""
    try:
        import joblib
        import json
        
        # Load model and features
        model = joblib.load('smart_hiring_model.pkl')
        with open('feature_columns.json', 'r') as f:
            features = json.load(f)
        
        # Test with sample data
        test_features = {
            'skill_match_score': 0.8,
            'avg_rating': 4.5,
            'recommendation_rate': 0.9,
            'completion_rate': 0.95,
            'bid_success_rate': 0.3,
            'category_experience': 5,
            'response_time_hours': 2.0,
            'portfolio_quality': 7.5,
            'budget_match_score': 0.85,
            'delivery_time_days': 7.0,
            'freelancer_tenure_days': 365.0,
            'project_complexity': 6.0,
            'client_history_score': 0.8,
            'past_collaboration': 1,
            'skills_count_match': 4,
            'workload_factor': 0.3,
            'mentorship_program_completed': 1  # Test with mentorship completed
        }
        
        # Create feature array
        feature_array = [test_features.get(f, 0.0) for f in features]
        
        # Make prediction
        prediction = model.predict_proba([feature_array])[0][1]
        
        print(f"\n🧪 Test Prediction:")
        print(f"Sample freelancer with mentorship completion: {prediction:.3f}")
        print(f"Features used: {len(features)}")
        
        return True
        
    except Exception as e:
        print(f"❌ Error testing model: {e}")
        return False

def main():
    print("🚀 Smart Hiring Model - Complete Workflow")
    print("=" * 50)
    
    # Step 1: Export data
    print("1️⃣ Exporting training data...")
    if not export_training_data():
        print("❌ Workflow failed at data export")
        return
    
    # Step 2: Train model
    print("\n2️⃣ Training model...")
    if not train_model():
        print("❌ Workflow failed at model training")
        return
    
    # Step 3: Test prediction
    print("\n3️⃣ Testing model...")
    if not test_prediction():
        print("❌ Workflow failed at prediction test")
        return
    
    print("\n✅ Complete workflow finished successfully!")
    print("Your model now includes mentorship completion feature!")

if __name__ == "__main__":
    main()