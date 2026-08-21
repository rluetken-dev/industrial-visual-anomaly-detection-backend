# Industrial Visual Anomaly Detection Backend – Development Status

## Purpose

This document records verified implementation progress and immediate next steps for the backend.

Stable requirements belong in `ProjectSpecification.md`, architecture in `ArchitectureOverview.md`, model integration in `ModelIntegrationStrategy.md`, and HTTP contracts in `ApiContract.md`.

## Current Phase

**Selectable multi-model backend integration complete**

The backend exposes a public model catalog, accepts an optional model identifier with image analysis, forwards explicit selection to Python, validates the returned actual model identity and result, and preserves compatibility when selection is omitted.

The implementation is committed in:

```text
af710d1 feat: support selectable inference models
```

The backend has been verified against the registry-capable model/inference service released as `v0.6.0`. The complete local catalog contained Capsule, Bottle, Candle, and Cashew. Native desktop integration exercised all four models, while the containerized stack explicitly verified Capsule and Cashew requests.

The backend feature branch is pushed, all automated tests pass, and local build and end-to-end verification succeed. Documentation and release preparation are the active milestone.

## Verified Environment

- Windows local development environment;
- .NET SDK `10.0.400`;
- Git `2.55.0.windows.3`;
- API target framework .NET 10;
- Debug and Release builds succeed;
- complete unit and integration suite succeeds;
- GitHub Actions succeeds for the pushed feature implementation;
- model-service `v0.6.0` provides the compatible catalog and selection contract;
- native backend integration is verified through HTTPS port `7056`;
- Docker integration is verified through backend host port `8080`;
- returned heatmaps decode as readable `320 × 320` PNG images.

## Implemented

### Repository and Build Foundation

- separate GitHub repository and `main` baseline;
- `.slnx`, production project, and xUnit test project;
- `src`, `tests`, `docs`, and `scripts` conventions;
- repository ignore, attribute, and formatting rules;
- Release restore, build, and test CI;
- Conventional Commit guidance.

### Public HTTP API

- `GET /health/live`;
- `GET /health/ready`;
- `GET /api/v1/models`;
- `POST /api/v1/analyses`;
- route-based `/api/v1` versioning;
- Development OpenAPI document;
- public catalog containing default model and available entries;
- optional multipart `modelId` analysis field;
- analysis response containing actual model, category, score, threshold, decision, processing time, trace ID, and Base64 PNG heatmap.

### Model Catalog

- `IInferenceModelCatalogProvider` application abstraction;
- validated `InferenceModelCatalog` application model;
- `PythonInferenceModelCatalogProvider` HTTP adapter;
- internal Python catalog DTO mapping;
- `ModelsController` public endpoint;
- configured `PythonInference:ModelCatalogPath`;
- validation of non-empty entries, unique identities, model metadata, and coherent default selection;
- catalog transport, timeout, unsuccessful response, malformed JSON, and invalid payload mapping to inference unavailable;
- catalog endpoint integration tests for success and dependency failure.

### Image Validation and Limits

- missing and empty upload rejection;
- PNG and JPEG media-type restriction;
- configurable file and multipart limits;
- PNG and JPEG signature validation;
- media-type/signature agreement;
- unreadable Python-decoded images mapped to `400`;
- startup validation of upload settings.

### Model-Specific Inference

- optional model ID carried by `AnalysisRequest` and `ImageAnalysisInput`;
- null, empty, or whitespace-only selection normalized to no explicit model;
- non-null model ID added to Python multipart requests as `modelId`;
- omitted selection preserves Python default behavior;
- response model ID and category validated and mapped;
- unknown-model Python failure currently mapped to public `503`;
- image, scalar decision, and heatmap contracts remain unchanged otherwise;
- cancellation and timeout semantics preserved.

### Heatmap Integration

- required heatmap metadata, dimensions, media type, and Base64 validation;
- internal heatmap mapped through `AnomalyHeatmap`;
- public Base64 PNG heatmap response;
- native WPF overlay verified with visibility and opacity controls;
- model-specific heatmaps verified for Capsule, Bottle, Candle, and Cashew.

### Health, Errors, and Observability

- dependency-independent liveness;
- Python-aware readiness;
- Problem Details boundary;
- invalid decoded image mapped to `400`;
- inference and catalog unavailable mapped to `503`;
- trace ID returned in success and errors;
- trace ID forwarded through `X-Correlation-ID`;
- structured analysis logging without raw image content;
- processing duration in analysis responses;
- constructor and domain invariants.

### Configuration and Client Integration

- image-upload, Python-inference, and CORS options bound and startup-validated;
- Python base URL, prediction path, catalog path, health path, and timeout configurable;
- explicit CORS allowlist with no browser origin by default;
- desktop catalog retrieval, default selection, explicit selection, and analysis verified;
- backend remains client-neutral and contains no WPF presentation logic.

### Testing

Unit tests cover:

- image validation;
- catalog domain invariants;
- Python catalog request, mapping, timeout, status, JSON, and payload failures;
- model-ID prediction forwarding;
- prediction, decision, and heatmap validation;
- health probing;
- exceptions and application invariants.

Integration tests cover:

- health endpoints;
- model-catalog endpoint success and failure;
- analysis and model-ID forwarding;
- public heatmap mapping;
- Problem Details;
- options binding and invalid catalog path startup behavior;
- dependency registration;
- CORS;
- OpenAPI.

The complete backend suite passes without datasets, artifacts, a registry, or a live Python process.

### End-to-End Verification

- direct Python catalog verified;
- backend catalog verified through `https://localhost:7056/api/v1/models`;
- four entries and Capsule default verified;
- native desktop retrieved and displayed the catalog;
- Capsule, Bottle, Candle, and Cashew selected successfully;
- decisions, model identity, category, timing, trace ID, and heatmaps displayed;
- Docker Compose loaded the same registry read-only;
- containerized backend catalog returned all four entries;
- explicit Capsule and Cashew containerized analyses succeeded;
- returned model ID matched each requested ID;
- verification script validated health, readiness, result, and decoded heatmap.

### Documentation

- README updated for catalog and model selection;
- API contract updated with catalog and optional `modelId`;
- architecture updated with catalog boundary and flows;
- model integration strategy and project specification aligned with Python `v0.6.0`;
- local and Docker verification behavior documented.

## Current Repository Shape

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
|   |-- Application/
|   |-- Contracts/
|   |-- Controllers/
|   |-- Errors/
|   |-- Infrastructure/
|   |-- Options/
|   |-- Validation/
|   |-- Program.cs
|   `-- appsettings.json
|-- tests/IndustrialVisualAnomalyDetection.Api.Tests/
|   |-- Integration/
|   `-- Unit/
|-- COMMITS.md
|-- IndustrialVisualAnomalyDetection.slnx
`-- README.md
```

## Verified HTTP Endpoints

```text
GET  /health/live
GET  /health/ready
GET  /api/v1/models
POST /api/v1/analyses
```

Analysis multipart fields:

```text
image   required
modelId optional
```

The catalog identifies the default and available models. A non-empty analysis `modelId` is forwarded to Python. Omitted or blank selection delegates to Python's configured default.

## Selected Integration Boundary

HTTP between ASP.NET Core and Python remains the selected boundary.

It provides:

- separation of public API and model runtime concerns;
- reuse of verified Python registry and inference behavior;
- independent repository testing and lifecycle management;
- replaceable adapters behind `IInferenceModelCatalogProvider`, `IAnomalyAnalyzer`, and `IInferenceServiceHealthProbe`;
- stable contracts for desktop and future web clients.

Direct .NET inference and ad hoc Python process invocation remain outside the current design.

## External Runtime Requirement

Complete inference requires compatible Python service source and externally supplied artifacts. Multi-model mode additionally requires `models.json`.

Model/inference service `v0.6.0` is the first published registry-capable compatibility baseline. Artifacts and registries remain outside this repository and are not redistributed by the backend.

Backend automated tests do not require the external runtime.

## Deferred or Optional Work

- dedicated public unknown-model error mapping;
- web client;
- production deployment configuration;
- monitoring, metrics, distributed tracing export, and alerting;
- rate limiting and abuse controls;
- authentication and authorization;
- persistence and analysis history;
- batch analysis;
- catalog caching or refresh policy;
- automatic visual category recognition;
- approved artifact distribution;
- deployment-like performance and load testing;
- additional localization forms.

Docker and Docker Compose packaging and multi-model selection are no longer deferred; they have been implemented and verified in the separate stack repository.

## Immediate Next Steps

1. Complete and commit the backend multi-model documentation update.
2. Run final Release build, complete automated tests, whitespace, and status checks.
3. Push documentation and verify GitHub Actions.
4. Publish an immutable multi-model backend release.
5. Update the stack to consume model `v0.6.0` and the new backend release tags.
6. Rebuild and verify the complete released stack.
7. Publish a new stack release only after released-component verification succeeds.

## Verification Commands

```powershell
dotnet build .\IndustrialVisualAnomalyDetection.slnx --configuration Release

dotnet test .\IndustrialVisualAnomalyDetection.slnx `
    --configuration Release `
    --no-build

git diff --check
git status --short --untracked-files=all
```

## Documentation Update Rule

Update this document after a verified milestone or meaningful group of changes. Do not record planned behavior as implemented. Avoid fixed automated-test counts because the suite changes frequently; record whether the complete suite passes instead.

## Last Updated

2026-08-21
