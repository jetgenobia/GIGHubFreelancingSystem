# Smart Hiring Random Forest Model Training Script (Local Version)
import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.metrics import classification_report, confusion_matrix, roc_auc_score, accuracy_score
import joblib
import json
import matplotlib.pyplot as plt
import seaborn as sns
import sys
import os

def load_training_data(csv_path=None):
    """Load training data from CSV file"""
    if csv_path is None:
        # Try to find CSV files in current directory
        csv_files = [f for f in os.listdir('.') if f.endswith('.csv') and 'training' in f.lower()]
        if not csv_files:
            print("No training data CSV found. Please export data first.")
            return None
        csv_path = csv_files[0]
        print(f"Using training data: {csv_path}")
    
    try:
        df = pd.read_csv(csv_path)
        print(f"Loaded {len(df)} training samples")
        return df
    except Exception as e:
        print(f"Error loading data: {e}")
        return None

def validate_features(df):
    """Validate that all required features are present"""
    required_features = [
        'skill_match_score', 'avg_rating', 'recommendation_rate', 'completion_rate',
        'bid_success_rate', 'category_experience', 'response_time_hours', 
        'portfolio_quality', 'budget_match_score', 'delivery_time_days',
        'freelancer_tenure_days', 'project_complexity', 'client_history_score',
        'past_collaboration', 'skills_count_match', 'workload_factor',
        'mentorship_program_completed',  # NEW FEATURE
        'is_successful_match'  # Target variable
    ]
    
    missing_features = [f for f in required_features if f not in df.columns]
    if missing_features:
        print(f"Missing features: {missing_features}")
        return False
    
    print(f"✓ All {len(required_features)-1} features present (+ target)")
    return True

def train_random_forest(df):
    """Train Random Forest model with updated features"""
    
    # Feature columns (17 features including mentorship)
    feature_columns = [
        'skill_match_score', 'avg_rating', 'recommendation_rate', 'completion_rate',
        'bid_success_rate', 'category_experience', 'response_time_hours', 
        'portfolio_quality', 'budget_match_score', 'delivery_time_days',
        'freelancer_tenure_days', 'project_complexity', 'client_history_score',
        'past_collaboration', 'skills_count_match', 'workload_factor',
        'mentorship_program_completed'  # NEW FEATURE
    ]
    
    # Prepare data
    X = df[feature_columns]
    y = df['is_successful_match']
    
    print(f"Training data shape: {X.shape}")
    print(f"Positive samples: {y.sum()}")
    print(f"Negative samples: {len(y) - y.sum()}")
    
    # Split data
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    
    # Train Random Forest
    print("Training Random Forest model...")
    rf_model = RandomForestClassifier(
        n_estimators=200,
        max_depth=15,
        min_samples_split=10,
        min_samples_leaf=5,
        max_features='sqrt',
        random_state=42,
        class_weight='balanced',
        n_jobs=-1  # Use all CPU cores
    )
    
    rf_model.fit(X_train, y_train)
    
    # Evaluate model
    train_accuracy = rf_model.score(X_train, y_train)
    test_accuracy = rf_model.score(X_test, y_test)
    
    # Cross-validation
    cv_scores = cross_val_score(rf_model, X_train, y_train, cv=5)
    
    # Predictions for detailed metrics
    y_pred = rf_model.predict(X_test)
    y_pred_proba = rf_model.predict_proba(X_test)[:, 1]
    
    print(f"\nModel Performance:")
    print(f"Training Accuracy: {train_accuracy:.4f}")
    print(f"Test Accuracy: {test_accuracy:.4f}")
    print(f"CV Score: {cv_scores.mean():.4f} (+/- {cv_scores.std() * 2:.4f})")
    
    if len(np.unique(y_test)) > 1:  # Check if we have both classes
        roc_score = roc_auc_score(y_test, y_pred_proba)
        print(f"ROC AUC Score: {roc_score:.4f}")
    
    print("\nClassification Report:")
    print(classification_report(y_test, y_pred))
    
    # Feature importance
    feature_importance = pd.DataFrame({
        'feature': feature_columns,
        'importance': rf_model.feature_importances_
    }).sort_values('importance', ascending=False)
    
    print("\nFeature Importance:")
    print(feature_importance)
    
    # Check mentorship feature importance
    mentorship_importance = feature_importance[
        feature_importance['feature'] == 'mentorship_program_completed'
    ]['importance'].iloc[0]
    print(f"\n🎓 Mentorship Program Completion Importance: {mentorship_importance:.4f}")
    
    return rf_model, feature_columns, feature_importance

def save_model(model, feature_columns, feature_importance):
    """Save the trained model and related files"""
    
    # Save model
    model_filename = 'smart_hiring_model.pkl'
    joblib.dump(model, model_filename)
    print(f"✓ Model saved as {model_filename}")
    
    # Save feature columns
    with open('feature_columns.json', 'w') as f:
        json.dump(feature_columns, f, indent=2)
    print("✓ Feature columns saved as feature_columns.json")
    
    # Save feature importance
    feature_importance.to_csv('feature_importance.csv', index=False)
    print("✓ Feature importance saved as feature_importance.csv")
    
    # Create model info file
    model_info = {
        "model_type": "RandomForestClassifier",
        "features_count": len(feature_columns),
        "features": feature_columns,
        "mentorship_included": True,
        "version": "2.0_with_mentorship"
    }
    
    with open('model_info.json', 'w') as f:
        json.dump(model_info, f, indent=2)
    print("✓ Model info saved as model_info.json")

def plot_feature_importance(feature_importance):
    """Create feature importance visualization"""
    plt.figure(figsize=(12, 8))
    
    # Create bar plot
    sns.barplot(data=feature_importance, x='importance', y='feature', palette='viridis')
    
    # Highlight mentorship feature
    for i, feature in enumerate(feature_importance['feature']):
        if feature == 'mentorship_program_completed':
            plt.gca().get_children()[i].set_color('#ff6b6b')  # Highlight in red
    
    plt.title('Smart Hiring Model - Feature Importance\n(Mentorship Feature Highlighted in Red)', 
              fontsize=14, fontweight='bold')
    plt.xlabel('Importance Score', fontweight='bold')
    plt.ylabel('Features', fontweight='bold')
    plt.tight_layout()
    
    # Save plot
    plt.savefig('feature_importance_plot.png', dpi=300, bbox_inches='tight')
    print("✓ Feature importance plot saved as feature_importance_plot.png")
    plt.show()

def main():
    print("🚀 Starting Smart Hiring Model Training (Local Version)")
    print("=" * 60)
    
    # Load training data
    csv_path = sys.argv[1] if len(sys.argv) > 1 else None
    df = load_training_data(csv_path)
    
    if df is None:
        print("❌ Training failed: No data available")
        return
    
    # Validate features
    if not validate_features(df):
        print("❌ Training failed: Missing required features")
        return
    
    # Train model
    print("\n🔧 Training Random Forest with 17 features...")
    model, feature_columns, feature_importance = train_random_forest(df)
    
    # Save model
    print("\n💾 Saving model artifacts...")
    save_model(model, feature_columns, feature_importance)
    
    # Create visualization
    print("\n📊 Creating visualizations...")
    plot_feature_importance(feature_importance)
    
    print("\n✅ Training completed successfully!")
    print(f"Model files created:")
    print("  - smart_hiring_model.pkl")
    print("  - feature_columns.json") 
    print("  - feature_importance.csv")
    print("  - model_info.json")
    print("  - feature_importance_plot.png")
    
    print(f"\n🎯 Your model now includes mentorship completion as a factor!")

if __name__ == '__main__':
    main()