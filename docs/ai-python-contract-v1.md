# AI Python Contract V1

## Internal Security
- Header required: `X-AI-Service-Key`
- Optional request field: `correlation_id`

## Enums
- `risk_level`: `LOW | MEDIUM | HIGH`
- `label`: `LOW | NORMAL | GOOD | EXCELLENT`
- `prediction_source`: `python_ai_service | csharp_fallback_rule`

## POST /predict/task-delay
### Request
```json
{
  "correlation_id": "ai-20260522-0001",
  "training_rows": [
    {
      "estimated_hours": 40,
      "spent_hours": 45,
      "progress_percent": 70,
      "priority_score": 3,
      "difficulty_score": 4,
      "days_until_deadline": 2,
      "late_days": 1
    }
  ],
  "input_features": {
    "estimated_hours": 32,
    "spent_hours": 20,
    "progress_percent": 60,
    "priority_score": 3,
    "difficulty_score": 4,
    "days_until_deadline": 1
  }
}
```

### Response
```json
{
  "correlation_id": "ai-20260522-0001",
  "estimated_days_late": 2.5,
  "risk_level": "MEDIUM",
  "model_name": "task_delay_linear_regression",
  "model_version": "v1",
  "prediction_source": "python_ai_service",
  "inference_time_ms": 16
}
```

### Rules
- Minimum `training_rows`: 2
- `estimated_days_late = max(0, predicted)`
- Risk mapping:
  - `LOW` if `estimated_days_late <= 0`
  - `MEDIUM` if `0 < estimated_days_late <= 3`
  - `HIGH` if `estimated_days_late > 3`

## POST /predict/performance
### Request
```json
{
  "correlation_id": "ai-20260522-0002",
  "training_rows": [
    {
      "kpi_score": 82,
      "completion_rate": 0.85,
      "late_rate": 0.1,
      "avg_progress": 88,
      "task_count": 12,
      "project_count": 2,
      "label": "GOOD"
    }
  ],
  "input_features": {
    "kpi_score": 78,
    "completion_rate": 0.8,
    "late_rate": 0.15,
    "avg_progress": 82,
    "task_count": 10,
    "project_count": 2
  }
}
```

### Response
```json
{
  "correlation_id": "ai-20260522-0002",
  "label": "GOOD",
  "confidence": 0.82,
  "model_name": "employee_performance_random_forest",
  "model_version": "v1",
  "prediction_source": "python_ai_service",
  "inference_time_ms": 21
}
```

### Rules
- Minimum `training_rows`: 4
- Must contain at least 2 unique labels

## GET /model/info
```json
{
  "service_name": "python-ai-service",
  "version": "1.0.0",
  "models": [
    {
      "name": "task_delay_linear_regression",
      "algorithm": "LinearRegression",
      "version": "v1"
    },
    {
      "name": "employee_performance_random_forest",
      "algorithm": "RandomForestClassifier",
      "version": "v1"
    }
  ]
}
```

## Standard Error Response
```json
{
  "error_code": "INSUFFICIENT_TRAINING_DATA",
  "message": "training_rows must contain at least 2 rows"
}
```

Error codes:
- `INSUFFICIENT_TRAINING_DATA`
- `INSUFFICIENT_CLASS_DIVERSITY`
- `INVALID_INPUT_SCHEMA`
- `UNAUTHORIZED`
