# Industrial Visual Anomaly Detection Backend – Development Status

## Purpose

This document records verified implementation progress and the immediate next steps for the backend.

It is intentionally concise. Stable requirements belong in `ProjectSpecification.md`, architecture in `ArchitectureOverview.md`, model integration in `ModelIntegrationStrategy.md`, and HTTP contracts in `ApiContract.md`.

## Current Phase

**Backend integration baseline complete**

The backend is ready to serve as the stable HTTP boundary for initial web-client development. It validates uploaded images, delegates inference to the Python model service, maps results and failures into the public API contract, and reports dependency-aware readiness.

Further backend work should now be driven by concrete client, deployment, or operational requirements rather than speculative expansion.

## Verified Environment

- operating system used for local development: Windows;
- .NET SDK: `10.0.400`;
- Git: `2.55.0.windows.3`;
- API target framework: .NET 10;
- repository maintained separately from the Python model repository;
- Debug and Release solution builds succeed;
- all automated unit and integration tests succeed;
- GitHub Actions CI succeeds on `main`;
- the complete local Python-service-to-backend analysis flow has been verified with a real MVTec AD image.

## Implemented

### Repository and Build Foundation

- Git repository initialized on the `main` branch and connected to GitHub;
- `IndustrialVisualAnomalyDetection.slnx` created;
- `src`, `tests`, `docs`, and `scripts` directory conventions established;
- controller-based ASP.NET Core Web API project created;
- xUnit API test project created and added to the solution;
- repository `.gitignore`, `.gitattributes`, and `.editorconfig` configured;
- generated WeatherForecast example removed;
- GitHub Actions CI added for restore, Release build, and automated tests;
- Conventional Commit guidelines documented.

### HTTP API

- liveness endpoint implemented at `GET /health/live`;
- dependency-aware readiness endpoint implemented at `GET /health/ready`;
- versioned analysis endpoint implemented at `POST /api/v1/analyses`;
- multipart image-upload contract implemented;
- response contract includes model identity, category, score, threshold, decision, processing time, and trace ID;
- route-based API version identifier established through `/api/v1`;
- OpenAPI document exposed in the Development environment;
- analysis operation ID, summary, description, multipart request, and binary image schema verified by automated tests.

### Image Validation and Request Limits

- missing and empty uploads rejected;
- supported media types restricted to PNG and JPEG by default;
- maximum image size configured and enforced;
- maximum multipart request-body size configured and enforced;
- PNG and JPEG file signatures validated before inference;
- media type and file signature must agree;
- unreadable image content returned by the Python service mapped to `400 Bad Request`;
- upload and request limits bound from validated startup configuration.

### Inference Integration

- application-level `IAnomalyAnalyzer` abstraction introduced;
- concrete HTTP adapter implemented for the Python FastAPI inference service;
- backend sends the validated image as multipart form data;
- Python inference responses validated before they enter the public API contract;
- invalid or unavailable inference responses mapped to controlled failures;
- configurable service base URL, prediction path, health path, and timeout implemented;
- inference-service health probe implemented;
- readiness reflects the actual availability of the Python inference service;
- model-runtime and artifact-loading logic remain owned by the Python repository.

### Error Handling and Observability

- ASP.NET Core Problem Details enabled;
- inference-unavailable failures mapped to `503 Service Unavailable`;
- invalid decoded image content mapped to `400 Bad Request`;
- validation failures returned with controlled status codes and messages;
- trace identifier included in Problem Details responses;
- `X-Correlation-ID` accepted and propagated when valid;
- generated trace identifier used when the caller supplies none;
- structured analysis logging added without logging raw image content;
- processing time included in successful analysis responses;
- constructor dependencies and domain inputs protected by explicit null and invariant checks.

### Configuration and Client Integration

- image-upload options bound and validated during startup;
- Python inference options bound and validated during startup;
- CORS origins bound and validated during startup;
- configurable CORS policy implemented;
- no browser origin allowed by default;
- explicitly configured origins supported for the future web client;
- invalid configuration prevents startup rather than failing during a request.

### Testing and Local Verification

- unit tests cover image validation, inference response handling, health probing, exceptions, and domain invariants;
- integration tests cover health endpoints, analysis behavior, Problem Details, startup options, CORS, and OpenAPI;
- backend tests run without a dataset or real model artifact;
- `scripts/verify-local-stack.ps1` verifies Python liveness, backend liveness, and backend readiness;
- the verification script optionally submits a real image through the backend analysis endpoint;
- a real Capsule `poke` image was classified as anomalous through the complete local stack;
- the verified response contained the expected model identity, category, score, threshold, decision, processing time, and trace ID.

### Documentation

- repository README updated with complete local setup and troubleshooting instructions;
- initial project specification, architecture overview, API contract, model-integration strategy, and development-status documents created;
- model artifact prerequisites and the two-repository startup sequence documented;
- local verification workflow documented.

## Current Repository Shape

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
|       |-- Application/
|       |-- Contracts/
|       |-- Controllers/
|       |-- Errors/
|       |-- Infrastructure/
|       |-- Options/
|       |-- Properties/
|       |-- Validation/
|       |-- Program.cs
|       |-- appsettings.Development.json
|       |-- appsettings.json
|       `-- IndustrialVisualAnomalyDetection.Api.csproj
|-- tests/
|   `-- IndustrialVisualAnomalyDetection.Api.Tests/
|       |-- Integration/
|       |-- Unit/
|       `-- IndustrialVisualAnomalyDetection.Api.Tests.csproj
|-- .editorconfig
|-- .gitattributes
|-- .gitignore
|-- COMMITS.md
|-- IndustrialVisualAnomalyDetection.slnx
`-- README.md
```

## Verified HTTP Endpoints

### Liveness

```text
GET /health/live
```

Returns HTTP `200 OK` while the backend process is running:

```json
{
  "status": "healthy"
}
```

### Readiness

```text
GET /health/ready
```

Returns HTTP `200 OK` when the configured Python inference service is healthy:

```json
{
  "status": "ready"
}
```

Returns HTTP `503 Service Unavailable` when the dependency cannot serve inference:

```json
{
  "status": "not_ready"
}
```

### Image Analysis

```text
POST /api/v1/analyses
Content-Type: multipart/form-data
Form field: image
```

The endpoint accepts a validated PNG or JPEG image and returns the mapped anomaly-analysis result. The verified local integration uses the Python service at `http://127.0.0.1:8000` and the backend HTTPS profile at `https://localhost:7056`.

## Selected Model-Integration Boundary

The backend communicates with the Python inference runtime over HTTP.

This decision is implemented and no longer open for the current baseline. The boundary provides:

- separation between ASP.NET Core API concerns and Python/PyTorch runtime concerns;
- reuse of the verified Python reference inference path;
- independent testing and lifecycle management;
- a replaceable adapter behind `IAnomalyAnalyzer`;
- a stable contract for future web and desktop clients.

Direct .NET inference and ad hoc Python process invocation are not part of the current implementation.

## External Runtime Requirement

Complete local inference requires the separate Python model repository and a compatible exported model artifact.

The artifact is not stored in this backend repository. The verified Capsule artifact contains `metadata.json` and `feature_memory.pt`, and its complete feature memory is approximately 410 MiB. Until an approved distribution mechanism exists, users must export it locally from their own permitted MVTec AD copy by following the model repository instructions.

Backend unit and integration tests do not require this external runtime.

## Deferred or Optional Work

The following items are deliberately deferred and do not block initial frontend development:

- web-client implementation;
- desktop-client implementation;
- Docker and Docker Compose packaging;
- production deployment configuration;
- production monitoring, metrics, tracing backend, and alerting;
- rate limiting and other deployment-specific abuse controls;
- authentication and authorization;
- persistence and analysis history;
- heatmap transport through the public backend contract;
- batch analysis;
- model selection across multiple artifacts or categories;
- an approved model-artifact distribution mechanism;
- performance and load testing under deployment-like conditions.

These capabilities should be added only when a verified product or deployment requirement justifies them.

## Immediate Next Steps

1. synchronize the remaining backend documentation with the completed integration baseline;
2. preserve the current API contract while implementing the first web client;
3. add only backend changes that are required by verified client integration needs;
4. perform another complete end-to-end verification after the first client flow is implemented;
5. evaluate Docker Compose and deployment documentation after the model service, backend, and client are stable together.

## Verification Commands

Build the solution:

```powershell
dotnet build .\IndustrialVisualAnomalyDetection.slnx
```

Run all automated tests:

```powershell
dotnet test .\IndustrialVisualAnomalyDetection.slnx
```

Build and test the Release configuration:

```powershell
dotnet build .\IndustrialVisualAnomalyDetection.slnx --configuration Release
dotnet test .\IndustrialVisualAnomalyDetection.slnx --configuration Release --no-build
```

Verify the running local stack:

```powershell
powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File .\scripts\verify-local-stack.ps1
```

Include a real image analysis:

```powershell
powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File .\scripts\verify-local-stack.ps1 `
    -ImagePath "C:\path\to\image.png"
```

## Documentation Update Rule

Update this document after a verified milestone or meaningful group of changes. Do not update it for every small internal edit.

Do not record planned behavior as implemented. Avoid fixed automated-test counts because the suite changes frequently; record whether the complete suite passes instead.

## Last Updated

2026-08-15
