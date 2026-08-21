# Industrial Visual Anomaly Detection Backend

[![CI](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/actions/workflows/ci.yml/badge.svg)](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Release](https://img.shields.io/github/v/release/rluetken-dev/industrial-visual-anomaly-detection-backend)](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/releases/latest)

ASP.NET Core backend for industrial visual anomaly detection, secure image-upload validation, selectable Python inference models, and client-neutral API integration.

The backend provides the public HTTP boundary for desktop and future web clients. Model development, registry validation, artifact loading, and inference execution remain in the separate [Python model repository](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model).

> **Current status:** The .NET 10 backend foundation, health checks, public model catalog, model-specific image analysis, upload validation, Python inference adapters, heatmap transport, readiness probing, OpenAPI contract, automated tests, CI, and local multi-model stack verification are implemented.

## Features

- versioned model-catalog and image-analysis APIs;
- discovery of available inference models through the Python service;
- optional per-request model selection through `modelId`;
- forwarding of selected model identifiers to Python;
- compatibility with clients that omit a model identifier;
- PNG and JPEG validation by size, media type, and signature;
- bounded multipart request handling;
- liveness and dependency-aware readiness endpoints;
- anomaly score, threshold, decision, model identity, processing time, trace ID, and Base64 PNG heatmap responses;
- Problem Details for validation and inference failures;
- trace-ID propagation through `X-Correlation-ID`;
- configurable Python catalog, prediction, and health paths;
- configurable CORS policy;
- Development OpenAPI document;
- unit and integration tests;
- GitHub Actions CI;
- verified native desktop and Docker Compose integration.

## System Overview

```text
Desktop or future web client
        |
        | GET models / POST analysis
        v
ASP.NET Core backend
        |
        | catalog provider + anomaly analyzer
        v
Python HTTP inference adapters
        |
        | GET models / POST prediction + modelId
        v
FastAPI inference service
        |
        v
Model registry and selected loaded artifact
```

The backend does not contain model-development logic, read `models.json`, or load PyTorch artifacts. Python remains authoritative for enabled models, default selection, artifact metadata, and inference execution. The backend validates and maps those internal contracts into stable public responses.

## Technology

- .NET 10
- ASP.NET Core Web API with controllers
- `HttpClient`-based Python integration
- dependency injection, configuration, options validation, Problem Details, CORS, and OpenAPI
- xUnit and `Microsoft.AspNetCore.Mvc.Testing`
- GitHub Actions
- PowerShell verification

## Repository Structure

```text
industrial-visual-anomaly-detection-backend/
|-- .github/workflows/ci.yml
|-- docs/
|   |-- ApiContract.md
|   |-- ArchitectureOverview.md
|   |-- DevelopmentStatus.md
|   |-- ModelIntegrationStrategy.md
|   `-- ProjectSpecification.md
|-- scripts/verify-local-stack.ps1
|-- src/IndustrialVisualAnomalyDetection.Api/
|-- tests/IndustrialVisualAnomalyDetection.Api.Tests/
|   |-- Integration/
|   `-- Unit/
|-- COMMITS.md
|-- IndustrialVisualAnomalyDetection.slnx
`-- README.md
```

## Prerequisites

For the backend alone:

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

For complete local inference:

- Python 3.12;
- model/inference service `v0.6.0` or a compatible later release;
- one or more compatible local model artifacts;
- a model registry for multi-model mode;
- an image to analyze.

## Build and Test

From the backend repository root:

```powershell
dotnet restore .\IndustrialVisualAnomalyDetection.slnx
dotnet build .\IndustrialVisualAnomalyDetection.slnx --configuration Release
dotnet test .\IndustrialVisualAnomalyDetection.slnx --configuration Release --no-build
```

The backend tests use controlled doubles and do not require a running Python service, dataset, registry, or artifact.

## Prepare the Python Model Service

Clone and install the model repository separately:

```powershell
git clone https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model.git
Set-Location .\industrial-visual-anomaly-detection-model
git checkout v0.6.0

python -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip setuptools wheel
.\.venv\Scripts\python.exe -m pip install -r .\requirements.txt
.\.venv\Scripts\python.exe -m pip install --editable .
```

Model artifacts and registries are intentionally excluded from Git. Create compatible artifacts by following the model-repository documentation and retain all dataset license obligations.

### Registry Layout

Example local layout:

```text
outputs/model-artifacts/
|-- models.json
|-- mvtec-ad-capsule-320/
|   |-- metadata.json
|   `-- feature_memory.pt
`-- visa-cashew-generalized-q95-320/
    |-- metadata.json
    `-- feature_memory.pt
```

Example `models.json`:

```json
{
  "schemaVersion": 1,
  "defaultModelId": "mvtec-ad-capsule-320",
  "models": [
    {
      "id": "mvtec-ad-capsule-320",
      "displayName": "MVTec AD - Capsule",
      "artifactDirectory": "mvtec-ad-capsule-320",
      "enabled": true
    },
    {
      "id": "visa-cashew-generalized-q95-320",
      "displayName": "VisA - Cashew",
      "artifactDirectory": "visa-cashew-generalized-q95-320",
      "enabled": true
    }
  ]
}
```

## Start the Complete Local Workflow

Use separate terminals for Python, the backend, and verification.

### 1. Start Python in Multi-Model Mode

From the model repository:

```powershell
$env:IVAD_MODEL_REGISTRY = "$PWD\outputs\model-artifacts\models.json"
Remove-Item Env:IVAD_MODEL_ARTIFACT -ErrorAction SilentlyContinue
$env:IVAD_MEMORY_CHUNK_SIZE = "4096"

.\.venv\Scripts\python.exe -m uvicorn `
    industrial_visual_anomaly_detection.service.app:app `
    --host 127.0.0.1 `
    --port 8000
```

Legacy single-artifact mode remains supported through `IVAD_MODEL_ARTIFACT`. Exactly one of `IVAD_MODEL_REGISTRY` and `IVAD_MODEL_ARTIFACT` must be configured.

### 2. Start the Backend

From the backend repository:

```powershell
dotnet run `
    --project .\src\IndustrialVisualAnomalyDetection.Api\IndustrialVisualAnomalyDetection.Api.csproj `
    --launch-profile https
```

The development profile uses:

```text
https://localhost:7056
http://localhost:5070
```

Trust the local certificate when necessary:

```powershell
dotnet dev-certs https --trust
```

### 3. Verify Health

```powershell
powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File .\scripts\verify-local-stack.ps1
```

The separate Docker Compose stack repository provides the preferred reproducible multi-model workflow and supports model-specific end-to-end verification.

## API Endpoints

```text
GET  /health/live
GET  /health/ready
GET  /api/v1/models
POST /api/v1/analyses
```

### Model Catalog

```powershell
Invoke-RestMethod `
    -Uri https://localhost:7056/api/v1/models `
    -Method Get |
    ConvertTo-Json -Depth 5
```

Example response:

```json
{
  "defaultModelId": "mvtec-ad-capsule-320",
  "models": [
    {
      "id": "mvtec-ad-capsule-320",
      "displayName": "MVTec AD - Capsule",
      "category": "capsule",
      "inputSize": 320,
      "isDefault": true
    },
    {
      "id": "visa-cashew-generalized-q95-320",
      "displayName": "VisA - Cashew",
      "category": "cashew",
      "inputSize": 320,
      "isDefault": false
    }
  ]
}
```

Python is authoritative for the catalog. The backend validates and maps it without maintaining a hard-coded list.

### Analyze with an Explicit Model

```powershell
curl.exe `
    --insecure `
    --request POST `
    https://localhost:7056/api/v1/analyses `
    --form "image=@C:\path\to\image.png;type=image/png" `
    --form "modelId=mvtec-ad-capsule-320"
```

The `modelId` field is optional for compatibility. When omitted, the Python service uses its configured default model.

Example response:

```json
{
  "model": {
    "id": "mvtec-ad-capsule-320",
    "category": "capsule"
  },
  "score": 4.992109,
  "threshold": 2.501822,
  "decision": "anomalous",
  "processingTimeMs": 1199,
  "traceId": "0HNNVDI4958NA:00000001",
  "heatmap": {
    "contentType": "image/png",
    "width": 320,
    "height": 320,
    "dataBase64": "<base64-encoded PNG>"
  }
}
```

Only PNG and JPEG uploads are accepted by default. The maximum file size is 10 MiB, and the maximum multipart request body is 11 MiB.

### Liveness and Readiness

```powershell
curl.exe --insecure https://localhost:7056/health/live
curl.exe --insecure https://localhost:7056/health/ready
```

Readiness returns `200 OK` with `{"status":"ready"}` when Python is reachable and `503 Service Unavailable` with `{"status":"not_ready"}` when it is unavailable.

## OpenAPI

The Development OpenAPI document is available at:

```text
https://localhost:7056/openapi/v1.json
```

It documents the catalog and analysis operations, multipart image and optional model fields, structured result, and Base64 heatmap response.

## Configuration

Defaults are stored in:

```text
src/IndustrialVisualAnomalyDetection.Api/appsettings.json
```

Main sections:

- `ImageUpload` controls file and request limits and allowed media types;
- `PythonInference` controls the base URL, prediction path, model-catalog path, health path, and timeout;
- `Cors` lists explicitly allowed origins.

Example overrides:

```powershell
$env:PythonInference__BaseUrl = "http://127.0.0.1:8000"
$env:PythonInference__PredictionPath = "/api/v1/predictions"
$env:PythonInference__ModelCatalogPath = "/api/v1/models"
$env:PythonInference__HealthPath = "/health/live"
$env:PythonInference__TimeoutSeconds = "30"
```

Invalid upload, inference, or CORS configuration is rejected during startup. Do not commit secrets, private service addresses, machine-specific artifact locations, images, or runtime output.

## Error Behavior

- invalid uploads return stable validation Problem Details;
- unavailable catalog or prediction dependencies map to service-unavailable behavior;
- malformed inference catalog or prediction payloads are rejected;
- unknown model identifiers are reported by Python and mapped through the backend failure boundary;
- successful analysis responses always identify the model actually used.

## Request Correlation

The backend uses the current ASP.NET Core trace identifier for each analysis. It forwards that value as `X-Correlation-ID` and includes it in analysis responses, Problem Details, and structured logs. It does not adopt a client-supplied header as its trace identifier.

## Common Problems

### Backend readiness returns 503

Confirm Python health and verify `PythonInference:BaseUrl` and `PythonInference:HealthPath`.

### Model catalog returns 503

Confirm that Python runs the registry-capable release, that `PythonInference:ModelCatalogPath` is `/api/v1/models`, and that the inference registry loaded successfully.

### Python startup fails

Configure exactly one of `IVAD_MODEL_REGISTRY` and `IVAD_MODEL_ARTIFACT`. In registry mode, confirm that `models.json` exists and every enabled relative artifact directory contains valid `metadata.json` and `feature_memory.pt` files.

### Analysis returns 400

Confirm that the upload is non-empty, supported, and has a matching signature. If `modelId` is supplied, it must not be blank.

### Analysis fails for one selected model

Retrieve `/api/v1/models` and use an enabled model ID exactly as returned. Display names and categories are not request identifiers.

### HTTPS certificate error

Trust the local development certificate with `dotnet dev-certs https --trust`. Use `curl.exe --insecure` only for local diagnostics.

## Continuous Integration

GitHub Actions restores dependencies, builds the solution in Release configuration, and runs all automated tests for pushes and pull requests targeting `main`.

Model artifacts, registries, and datasets are not required by backend CI.

## Related Repositories

- [Model and inference service](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model)
- [Docker Compose stack](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-stack)

## Documentation

- [Project Specification](docs/ProjectSpecification.md)
- [Architecture Overview](docs/ArchitectureOverview.md)
- [API Contract](docs/ApiContract.md)
- [Model Integration Strategy](docs/ModelIntegrationStrategy.md)
- [Development Status](docs/DevelopmentStatus.md)
- [Commit Message Guidelines](COMMITS.md)

## Security and Privacy

Uploaded images are untrusted input. Request size, file size, media type, and signature are validated before inference. Raw images are not persisted or logged by default.

The backend does not read the registry or artifacts directly. This repository contains no datasets, generated model artifacts, registries, secrets, or production credentials.

## Responsible Use

This project is an experimental educational portfolio system. It is not a certified industrial inspection system and must not autonomously make production acceptance, safety, medical, or regulatory decisions.

## License

No source-code license has been selected yet. Until a license is added, default copyright restrictions apply. Model artifacts, pretrained weights, datasets, and third-party content remain subject to their own terms.
