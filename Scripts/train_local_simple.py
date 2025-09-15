# Simple Local Training Script for Smart Hiring Model
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
import joblib
import sys
import json

def main():
    # Load CSV data
    csv_file = sys.argv[1] if len(sys.argv) > 1 else 'smart_hiring_training_data.csv'
    
    try:
        df = pd.read_csv(csv_file)
        print(f"Loaded {len(df)} training samples from {csv_file}")
    except:
        print(f"Error: Could not load {csv_file}")
        return
    
    # Updated feature columns (17 total with mentorship)
    features = [
        'skill_match_score', 'avg_rating', 'recommendation_rate', 'completion_rate',
        'bid_success_rate', 'category_experience', 'response_time_hours', 'portfolio_quality',
        'budget_match_score', 'delivery_time_days', 'freelancer_tenure_days', 'project_complexity',
        'client_history_score', 'past_collaboration', 'skills_count_match', 'workload_factor',
        'mentorship_program_completed'  # NEW FEATURE
    ]
    
    # Prepare data
    X = df[features]
    y = df['is_successful_match']
    
    # Train model
    print("Training Random Forest...")
    model = RandomForestClassifier(n_estimators=100, random_state=42)
    model.fit(X, y)
    
    # Save model
    joblib.dump(model, 'smart_hiring_model.pkl')
    
    # Save feature info
    with open('feature_columns.json', 'w') as f:
        json.dump(features, f)
    
    accuracy = model.score(X, y)
    print(f"Model trained! Training accuracy: {accuracy:.3f}")
    print(f"Features: {len(features)} (including mentorship completion)")
    print("Files saved: smart_hiring_model.pkl, feature_columns.json")
    
    # Show feature importance
    importance = model.feature_importances_
    mentorship_idx = features.index('mentorship_program_completed')
    print(f"Mentorship completion importance: {importance[mentorship_idx]:.4f}")

if __name__ == "__main__":
    main()