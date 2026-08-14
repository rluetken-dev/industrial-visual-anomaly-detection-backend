# Industrial Visual Anomaly Detection Backend

[![CI](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/actions/workflows/ci.yml/badge.svg)](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/actions/workflows/ci.yml)

ASP.NET Core backend for industrial visual anomaly detection, secure image-upload validation, Python model-service orchestration, and client-neutral API integration.

The backend provides a stable HTTP boundary for web and desktop clients. Model development, evaluation, artifact export, and the internal inference runtime remain in the separate [Python model repository](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model).

> **Current status:** The .NET 10 backend foundation, health checks, image-analysis endpoint, upload validation, Python inference adapter, readiness probing, OpenAPI contract, automated tests, CI, and local stack verification are implemented.

## Features

- versioned image-analysis API;
- PNG and JPEG upload validation by size, media type, and file signature;
- bounded multipart request handling;
- Python inference integration through a dedicated HTTP adapter;
- liveness and dependency-aware readiness endpoints;
- anomaly score, threshold, decision, model identity, processing time, and trace ID responses;
- Problem Details responses for validation and inference failures;
- request correlation through `X-Correlation-ID`;
- configurable CORS policy;
- generated OpenAPI document in the Development environment;
- unit and integration tests;
- GitHub Actions build and test workflow;
- PowerShell verification of the complete local stack.

## System Overview

```text
Web/Desktop client
        |
        v
ASP.NET Core backend
        |
        v
Application inference abstraction
        |
        v
Python HTTP inference adapter
        |
        v
FastAPI inference service
        |
        v
Exported PyTorch model artifact
```

The backend does not contain model-development logic or load PyTorch artifacts directly. This separation keeps the public API independent from the Python runtime and allows the inference implementation to evolve behind a stable contract.

## Technology

- .NET 10
- ASP.NET Core Web API with controllers
- `HttpClient`-based Python service integration
- built-in dependency injection, configuration, options validation, Problem Details, CORS, and OpenAPI
- xUnit and `Microsoft.AspNetCore.Mvc.Testing`
- GitHub Actions
- PowerShell local verification

## Repository Structure

```text
industrial-visual-anomaly-detection-backend/
|-- .github/
|   `-- workflows/
|       `-- ci.yml
|-- docs/
|   |-- ApiContract.md
|   |-- ArchitectureOverview.md
|   |-- DevelopmentStatus.md
|   |-- ModelIntegrationStrategy.md
|   `-- ProjectSpecification.md
|-- scripts/
|   `-- verify-local-stack.ps1
|-- src/
|   `-- IndustrialVisualAnomalyDetection.Api/
|-- tests/
|   `-- IndustrialVisualAnomalyDetection.Api.Tests/
|       |-- Integration/
|       `-- Unit/
|-- COMMITS.md
|-- IndustrialVisualAnomalyDetection.slnx
`-- README.md
```

## Prerequisites

For the backend alone:

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

For complete local inference:

- Python 3.12
- the cloned Python model repository;
- an exported compatible model artifact;
- an image to analyze.

Verify the main tools:

```powershell
dotnet --version
python --version
git --version
```

## Clone the Repositories

Keep the backend and model repositories as separate sibling directories:

```powershell
git clone https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend.git
git clone https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model.git
```

Example layout:

```text
projects/
|-- industrial-visual-anomaly-detection-backend/
`-- industrial-visual-anomaly-detection-model/
```

## Build and Test the Backend

From the backend repository root:

```powershell
dotnet restore .\IndustrialVisualAnomalyDetection.slnx
dotnet build .\IndustrialVisualAnomalyDetection.slnx
dotnet test .\IndustrialVisualAnomalyDetection.slnx --no-build
```

The ordinary backend test suite uses controlled test doubles and does not require a running Python service, a dataset, or a model artifact.

## Prepare the Python Model Service

From the model repository root, create its virtual environment and install the dependencies:

```powershell
python -m venv .venv

.\.venv\Scripts\python.exe -m pip install --upgrade pip setuptools wheel
.\.venv\Scripts\python.exe -m pip install -r .\requirements.txt
.\.venv\Scripts\python.exe -m pip install --editable .
```

### Model Artifact Requirement

Model artifacts are intentionally excluded from Git. The verified Capsule artifact contains a large feature memory and depends on MVTec AD data, whose redistribution terms must be considered separately.

At present, create the artifact locally from your own MVTec AD copy. Download [MVTec AD](https://www.mvtec.com/research-teaching/datasets/mvtec-ad), extract it outside both repositories, and run:

```powershell
.\.venv\Scripts\python.exe .\scripts\export_mvtec_ad_model.py `
    --dataset-root C:\path\to\mvtec-ad `
    --manifest .\configs\splits\mvtec-ad-capsule-seed-42.json `
    --output-directory .\outputs\model-artifacts\mvtec-ad-capsule-320 `
    --input-size 320 `
    --top-fraction 0.01 `
    --memory-fraction 1.0 `
    --sampling-seed 42
```

The resulting directory contains:

```text
metadata.json
feature_memory.pt
```

The complete feature memory is approximately 410 MiB. Artifact export and threshold calculation can take some time on CPU.

## Start the Complete Local Stack

Use two terminals and keep both processes running.

### 1. Start the Python Inference Service

In the model repository:

```powershell
$env:IVAD_MODEL_ARTIFACT = "$PWD\outputs\model-artifacts\mvtec-ad-capsule-320"
$env:IVAD_MEMORY_CHUNK_SIZE = "4096"

.\.venv\Scripts\python.exe -m uvicorn `
    industrial_visual_anomaly_detection.service.app:app `
    --host 127.0.0.1 `
    --port 8000
```

The service loads the model artifact during startup. Its default liveness URL is:

```text
http://127.0.0.1:8000/health/live
```

### 2. Start the ASP.NET Core Backend

In the backend repository:

```powershell
dotnet run `
    --project .\src\IndustrialVisualAnomalyDetection.Api\IndustrialVisualAnomalyDetection.Api.csproj `
    --launch-profile https
```

The checked-in development profile uses:

```text
https://localhost:7056
http://localhost:5070
```

If the local HTTPS development certificate is not trusted yet, run:

```powershell
dotnet dev-certs https --trust
```

### 3. Verify the Stack

With both services running, execute from the backend repository:

```powershell
powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File .\scripts\verify-local-stack.ps1
```

This checks:

- Python service liveness;
- backend liveness;
- backend readiness.

To include a real end-to-end analysis:

```powershell
powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File .\scripts\verify-local-stack.ps1 `
    -ImagePath "C:\path\to\image.png"
```

The script prints the model ID, category, anomaly score, threshold, decision, processing time, and trace ID.

## API Endpoints

```text
GET  /health/live
GET  /health/ready
POST /api/v1/analyses
```

### Liveness

```powershell
curl.exe --insecure https://localhost:7056/health/live
```

Expected response:

```json
{
  "status": "healthy"
}
```

### Readiness

```powershell
curl.exe --insecure https://localhost:7056/health/ready
```

The endpoint returns `200 OK` with `{"status":"ready"}` when the Python service is reachable. It returns `503 Service Unavailable` with `{"status":"not_ready"}` when the dependency is unavailable.

### Analyze an Image

```powershell
curl.exe `
    --insecure `
    -X POST `
    https://localhost:7056/api/v1/analyses `
    -F "image=@C:\path\to\image.png;type=image/png"
```

Example response:

```json
{
  "model": {
    "id": "mvtec-ad-capsule-320",
    "category": "capsule"
  },
  "score": 4.992109298706055,
  "threshold": 2.501821517944336,
  "decision": "anomalous",
  "processingTimeMs": 1692,
  "traceId": "0HNNQ2F8C9UQT:00000001"
}
```

Only PNG and JPEG uploads are accepted by default. The configured maximum file size is 10 MiB, and the maximum multipart request body is 11 MiB.

## OpenAPI

The OpenAPI document is available in the Development environment:

```text
https://localhost:7056/openapi/v1.json
```

The analysis operation documents its `multipart/form-data` request and binary image field.

## Configuration

Default configuration is stored in:

```text
src/IndustrialVisualAnomalyDetection.Api/appsettings.json
```

Main sections:

- `ImageUpload` controls file size, request size, and allowed media types;
- `PythonInference` controls the service URL, paths, and timeout;
- `Cors` lists explicitly allowed client origins.

ASP.NET Core environment variables can override individual values. Double underscores represent nested configuration keys.

Example:

```powershell
$env:PythonInference__BaseUrl = "http://127.0.0.1:8000"
$env:PythonInference__TimeoutSeconds = "30"
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
```

Invalid upload, inference, or CORS configuration is rejected during application startup.

Do not commit secrets, private service addresses, machine-specific artifact locations, uploaded images, or generated runtime output.

## Request Correlation

Clients may send an `X-Correlation-ID` header. The backend propagates a valid supplied identifier or creates a trace identifier when none is provided. The identifier is included in analysis responses, Problem Details responses, and structured logs.

## Common Problems

### Backend readiness returns 503

Confirm that the Python service is running and healthy:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:8000/health/live
```

Then verify that `PythonInference:BaseUrl` points to the same address.

### Backend port 7056 cannot be reached

The backend process is not running, a different launch profile is active, or the configured port changed. Start the backend with the `https` launch profile and use the URLs printed at startup.

### Python service fails during startup

Check that `IVAD_MODEL_ARTIFACT` points to a directory containing both `metadata.json` and `feature_memory.pt`, and that the virtual environment contains all dependencies.

### HTTPS certificate error

Trust the local development certificate with `dotnet dev-certs https --trust`. For local command-line diagnostics only, `curl.exe --insecure` can bypass certificate verification.

### Analysis request returns 400

Confirm that the upload is non-empty, uses `image/png` or `image/jpeg`, and has a matching PNG or JPEG file signature. A renamed or unreadable file is rejected.

## Continuous Integration

The GitHub Actions workflow restores dependencies, builds the solution in Release configuration, and runs all automated tests for pushes and pull requests targeting `main`.

Model artifacts and datasets are not required by backend CI.

## Related Repository

[Industrial Visual Anomaly Detection Model](https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model)

The model repository owns:

- dataset validation and deterministic splits;
- preprocessing and frozen feature extraction;
- feature-memory construction and sampling;
- anomaly scoring, evaluation, and threshold selection;
- heatmap generation;
- versioned artifact export and loading;
- reference inference and the internal FastAPI service.

## Documentation

- [Project Specification](docs/ProjectSpecification.md)
- [Architecture Overview](docs/ArchitectureOverview.md)
- [API Contract](docs/ApiContract.md)
- [Model Integration Strategy](docs/ModelIntegrationStrategy.md)
- [Development Status](docs/DevelopmentStatus.md)
- [Commit Message Guidelines](COMMITS.md)

## Security and Privacy

Uploaded images are treated as untrusted input. Request size, file size, media type, and file signature are validated before inference. Raw images are not persisted or logged by default.

This repository does not contain datasets, generated model artifacts, secrets, or production credentials.

## Responsible Use

This project is an experimental and educational portfolio system. It is not a certified industrial inspection system and must not autonomously make production acceptance, safety, medical, or regulatory decisions.

## License

No source-code license has been selected yet. Until a license is added, default copyright restrictions apply. Model artifacts, pretrained weights, datasets, and other third-party content remain subject to their own terms.
