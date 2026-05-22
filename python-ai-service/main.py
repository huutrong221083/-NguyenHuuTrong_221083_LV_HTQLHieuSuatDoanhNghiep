from __future__ import annotations

import time
import os
from typing import Literal

import numpy as np
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LinearRegression

SERVICE_NAME = "python-ai-service"
SERVICE_VERSION = "1.0.0"
TASK_DELAY_MODEL = "task_delay_linear_regression"
PERFORMANCE_MODEL = "employee_performance_random_forest"
MODEL_VERSION = "v1"
SERVICE_API_KEY = os.getenv("AI_SERVICE_KEY", "dev-ai-service-key")

app = FastAPI(title=SERVICE_NAME, version=SERVICE_VERSION)


class ErrorResponse(BaseModel):
    error_code: str
    message: str


class TaskDelayTrainingRow(BaseModel):
    estimated_hours: float
    spent_hours: float
    progress_percent: float
    priority_score: float
    difficulty_score: float
    days_until_deadline: float
    late_days: float


class TaskDelayInputFeatures(BaseModel):
    estimated_hours: float
    spent_hours: float
    progress_percent: float
    priority_score: float
    difficulty_score: float
    days_until_deadline: float


class TaskDelayPredictRequest(BaseModel):
    correlation_id: str | None = None
    training_rows: list[TaskDelayTrainingRow]
    input_features: TaskDelayInputFeatures


class TaskDelayPredictResponse(BaseModel):
    correlation_id: str | None = None
    estimated_days_late: float
    risk_level: Literal["LOW", "MEDIUM", "HIGH"]
    model_name: str
    model_version: str
    prediction_source: Literal["python_ai_service"]
    inference_time_ms: int


class PerformanceTrainingRow(BaseModel):
    kpi_score: float
    completion_rate: float
    late_rate: float
    avg_progress: float
    task_count: float
    project_count: float
    label: Literal["LOW", "NORMAL", "GOOD", "EXCELLENT"]


class PerformanceInputFeatures(BaseModel):
    kpi_score: float
    completion_rate: float
    late_rate: float
    avg_progress: float
    task_count: float
    project_count: float


class PerformancePredictRequest(BaseModel):
    correlation_id: str | None = None
    training_rows: list[PerformanceTrainingRow]
    input_features: PerformanceInputFeatures


class PerformancePredictResponse(BaseModel):
    correlation_id: str | None = None
    label: Literal["LOW", "NORMAL", "GOOD", "EXCELLENT"]
    confidence: float
    model_name: str
    model_version: str
    prediction_source: Literal["python_ai_service"]
    inference_time_ms: int


class ModelInfoResponse(BaseModel):
    service_name: str
    version: str
    models: list[dict[str, str]]


def _require_internal_key(header_key: str | None, expected_key: str | None) -> None:
    if not expected_key:
        return
    if not header_key or header_key != expected_key:
        raise HTTPException(status_code=401, detail=ErrorResponse(error_code="UNAUTHORIZED", message="Invalid AI service key").model_dump())


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": SERVICE_NAME, "version": SERVICE_VERSION}


@app.get("/model/info", response_model=ModelInfoResponse)
def model_info() -> ModelInfoResponse:
    return ModelInfoResponse(
        service_name=SERVICE_NAME,
        version=SERVICE_VERSION,
        models=[
            {"name": TASK_DELAY_MODEL, "algorithm": "LinearRegression", "version": MODEL_VERSION},
            {"name": PERFORMANCE_MODEL, "algorithm": "RandomForestClassifier", "version": MODEL_VERSION},
        ],
    )


@app.post("/predict/task-delay", response_model=TaskDelayPredictResponse, responses={400: {"model": ErrorResponse}, 401: {"model": ErrorResponse}, 422: {"model": ErrorResponse}})
def predict_task_delay(
    request: TaskDelayPredictRequest,
    x_ai_service_key: str | None = Header(default=None),
) -> TaskDelayPredictResponse:
    _require_internal_key(x_ai_service_key, SERVICE_API_KEY)

    if len(request.training_rows) < 2:
        raise HTTPException(
            status_code=400,
            detail=ErrorResponse(
                error_code="INSUFFICIENT_TRAINING_DATA",
                message="training_rows must contain at least 2 rows",
            ).model_dump(),
        )

    started = time.perf_counter()

    x_train = np.array([
        [
            row.estimated_hours,
            row.spent_hours,
            row.progress_percent,
            row.priority_score,
            row.difficulty_score,
            row.days_until_deadline,
        ]
        for row in request.training_rows
    ])
    y_train = np.array([row.late_days for row in request.training_rows])

    model = LinearRegression()
    model.fit(x_train, y_train)

    x_input = np.array([
        [
            request.input_features.estimated_hours,
            request.input_features.spent_hours,
            request.input_features.progress_percent,
            request.input_features.priority_score,
            request.input_features.difficulty_score,
            request.input_features.days_until_deadline,
        ]
    ])

    predicted = float(model.predict(x_input)[0])
    estimated_days_late = round(max(0.0, predicted), 2)
    risk_level: Literal["LOW", "MEDIUM", "HIGH"]
    if estimated_days_late <= 0:
        risk_level = "LOW"
    elif estimated_days_late <= 3:
        risk_level = "MEDIUM"
    else:
        risk_level = "HIGH"

    elapsed = int((time.perf_counter() - started) * 1000)

    return TaskDelayPredictResponse(
        correlation_id=request.correlation_id,
        estimated_days_late=estimated_days_late,
        risk_level=risk_level,
        model_name=TASK_DELAY_MODEL,
        model_version=MODEL_VERSION,
        prediction_source="python_ai_service",
        inference_time_ms=max(1, elapsed),
    )


@app.post("/predict/performance", response_model=PerformancePredictResponse, responses={400: {"model": ErrorResponse}, 401: {"model": ErrorResponse}, 422: {"model": ErrorResponse}})
def predict_performance(
    request: PerformancePredictRequest,
    x_ai_service_key: str | None = Header(default=None),
) -> PerformancePredictResponse:
    _require_internal_key(x_ai_service_key, SERVICE_API_KEY)

    if len(request.training_rows) < 4:
        raise HTTPException(
            status_code=400,
            detail=ErrorResponse(
                error_code="INSUFFICIENT_TRAINING_DATA",
                message="training_rows must contain at least 4 rows",
            ).model_dump(),
        )

    labels = [row.label for row in request.training_rows]
    unique_labels = set(labels)
    if len(unique_labels) < 2:
        raise HTTPException(
            status_code=400,
            detail=ErrorResponse(
                error_code="INSUFFICIENT_CLASS_DIVERSITY",
                message="training data must contain at least 2 different labels",
            ).model_dump(),
        )

    started = time.perf_counter()

    x_train = np.array([
        [
            row.kpi_score,
            row.completion_rate,
            row.late_rate,
            row.avg_progress,
            row.task_count,
            row.project_count,
        ]
        for row in request.training_rows
    ])
    y_train = np.array(labels)

    model = RandomForestClassifier(n_estimators=100, random_state=42)
    model.fit(x_train, y_train)

    x_input = np.array([
        [
            request.input_features.kpi_score,
            request.input_features.completion_rate,
            request.input_features.late_rate,
            request.input_features.avg_progress,
            request.input_features.task_count,
            request.input_features.project_count,
        ]
    ])

    label = str(model.predict(x_input)[0])
    probabilities = model.predict_proba(x_input)[0]
    confidence = float(np.max(probabilities)) if len(probabilities) > 0 else 0.0

    elapsed = int((time.perf_counter() - started) * 1000)

    return PerformancePredictResponse(
        correlation_id=request.correlation_id,
        label=label,  # type: ignore[arg-type]
        confidence=round(confidence, 4),
        model_name=PERFORMANCE_MODEL,
        model_version=MODEL_VERSION,
        prediction_source="python_ai_service",
        inference_time_ms=max(1, elapsed),
    )
