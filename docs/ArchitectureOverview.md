# Industrial Visual Anomaly Detection Backend – Architecture Overview

## Purpose

This document describes the backend's implemented component boundaries, dependency direction, request flows, and integration points.

Detailed HTTP schemas belong in `ApiContract.md`; verified progress belongs in `DevelopmentStatus.md`.

## System Context

The backend is the stable public boundary between clients and the internal Python model runtime.

```text
+----------------+       +------------------------------+
| Desktop client |------>| ASP.NET Core backend         |
+----------------+       |                              |
                         | catalog, validation,         |
+----------------+       | orchestration, mapping       |
| Future web     |------>|                              |
+----------------+       +---------------+--------------+
                                         |
                                         | HTTP
                                         | GET models
                                         | POST image + modelId
                                         v
                         +------------------------------+
                         | Python FastAPI service       |
                         | registry and inference       |
                         +---------------+--------------+
                                         |
                                         v
                         +------------------------------+
                         | Enabled model artifacts      |
                         | metadata + feature memories  |
                         +------------------------------+
```

Clients do not read registries or artifacts, reproduce preprocessing, calculate thresholds, or implement anomaly decisions. They retrieve an available model catalog and submit an image with an optional stable model identifier.

## Repository Boundaries

### Backend Repository

Owns:

- public catalog, analysis, and health APIs;
- transport and upload validation;
- request-size limits;
- application catalog and analysis abstractions;
- Python HTTP communication;
- response and failure mapping;
- liveness and readiness;
- CORS, configuration, OpenAPI, logging, and backend tests.

### Model Repository

Owns:

- datasets, fitting, evaluation, and artifacts;
- model-registry schema and validation;
- enabled and default model selection;
- artifact and runtime loading;
- preprocessing, scoring, thresholds, decisions, and heatmaps;
- internal FastAPI catalog and prediction endpoints.

The backend does not duplicate Python/PyTorch logic or maintain a hard-coded model list. HTTP contracts separate the repositories.

### Desktop and Future Clients

Clients own presentation, selection state, image interaction, and heatmap display. They communicate only with the backend and treat the public catalog and analysis response as authoritative.

### Stack Repository

The stack repository owns Linux image builds, Compose networking, health-based startup ordering, read-only registry and artifact mounting, host ports, and end-to-end verification.

## Production Project

The solution currently uses one production project because the deployment unit is small and logical boundaries are explicit.

```text
IndustrialVisualAnomalyDetection.Api/
|-- Application/
|   |-- Analysis/
|   |-- Health/
|   `-- Models/
|-- Contracts/
|   |-- Analyses/
|   |-- Health/
|   `-- Models/
|-- Controllers/
|-- Errors/
|-- Infrastructure/
|   `-- Inference/
|-- Options/
|-- Validation/Images/
|-- Program.cs
|-- appsettings.json
`-- IndustrialVisualAnomalyDetection.Api.csproj
```

Additional .NET projects should be introduced only when separation produces a measurable maintenance or deployment benefit.

## Logical Components

### HTTP and Controller Layer

Implemented by `ModelsController`, `AnalysesController`, `HealthController`, and public contracts.

Responsibilities:

- expose public routes;
- bind multipart image and optional model selection;
- invoke transport validation;
- invoke catalog and analysis abstractions;
- map application results into public responses;
- map direct validation failures;
- describe OpenAPI behavior;
- measure and log analysis duration.

Controllers do not read registries, execute inference, parse artifact metadata, or calculate thresholds.

### Validation Layer

`IImageUploadValidator` and `ImageUploadValidator` require an image, reject empty content, enforce size limits, restrict media types, inspect signatures, and verify media-type/signature agreement.

Complete image decoding remains Python's responsibility. Optional `modelId` binding is normalized by the controller: null, empty, or whitespace-only input becomes no explicit selection.

### Application Analysis Boundary

Implemented by:

- `IAnomalyAnalyzer`;
- `ImageAnalysisInput`;
- `AnomalyAnalysisResult`;
- `AnomalyHeatmap`;
- typed analysis exceptions.

`ImageAnalysisInput` carries the validated stream, media type, trace ID, and optional model ID. `AnomalyAnalysisResult` carries the actual model used, category, score, threshold, decision, and heatmap without exposing HTTP or Python DTOs.

The application types enforce null, text, numeric, and heatmap invariants so invalid state cannot silently cross boundaries.

### Application Model-Catalog Boundary

Implemented by:

- `IInferenceModelCatalogProvider`;
- `InferenceModelCatalog`;
- model entry value types.

This abstraction supplies the validated available-model collection and default model to `ModelsController`. The public controller depends on this application contract rather than Python DTOs or `HttpClient`.

The application catalog enforces a non-empty model set, unique identifiers, valid entry data, and coherent default selection.

### Application Health Boundary

`IInferenceServiceHealthProbe` separates health semantics from HTTP implementation. Liveness is local; readiness uses the probe to determine whether Python is reachable.

### Python Prediction Adapter

`PythonServiceAnomalyAnalyzer`:

- posts validated image content to the configured prediction path;
- conditionally adds multipart `modelId`;
- forwards `X-Correlation-ID`;
- applies the configured timeout;
- maps Python image `400` to invalid image;
- maps other unsuccessful statuses to inference unavailable;
- deserializes and validates model identity, numeric values, decision consistency, and heatmap;
- returns `AnomalyAnalysisResult`;
- preserves caller cancellation while mapping dependency timeouts.

The adapter does not pre-fetch or locally validate catalog membership. Python remains authoritative for resolving the requested model.

### Python Model-Catalog Adapter

`PythonInferenceModelCatalogProvider`:

- calls the configured Python model-catalog path;
- applies the shared inference timeout policy;
- deserializes the internal catalog DTO;
- maps model identity, display name, category, input size, and default state;
- validates catalog consistency through application types;
- maps transport, timeout, unsuccessful status, malformed JSON, and invalid payloads to inference unavailable;
- preserves caller-request cancellation.

This adapter is the only catalog component that knows the Python catalog protocol.

### Python Health Adapter

`PythonInferenceServiceHealthProbe` calls the configured health path, reports readiness for successful responses, reports not ready for connection and dependency timeout failures, and preserves caller cancellation.

It deliberately does not retrieve the catalog or execute prediction on every readiness request.

### Error Boundary

ASP.NET Core Problem Details and typed exception handlers:

- map unreadable decoded images to `400`;
- map unavailable, invalid, timed-out, or unsuccessful inference behavior to `503`;
- include backend trace identifiers;
- prevent stack traces, paths, registry contents, and Python internals from entering public responses.

The current public contract maps an unknown model returned by Python as inference unavailable (`503`). A dedicated public unknown-model mapping is deferred.

### Configuration Boundary

`ImageUploadOptions` controls file size, multipart size, and allowed media types.

`PythonInferenceOptions` controls:

- service base URL;
- prediction path;
- model-catalog path;
- health path;
- request timeout.

`ApiCorsOptions` controls explicit browser origins.

All option groups are validated during startup. Inference paths must be non-empty and begin with `/`. Invalid configuration prevents startup rather than producing delayed request failures.

## Dependency Direction

```text
ModelsController
        |
        `----> IInferenceModelCatalogProvider
                         ^
                         |
             PythonInferenceModelCatalogProvider

AnalysesController
        |
        +----> IImageUploadValidator <---- ImageUploadValidator
        |
        `----> IAnomalyAnalyzer <--------- PythonServiceAnomalyAnalyzer

HealthController
        |
        `----> IInferenceServiceHealthProbe
                         ^
                         |
             PythonInferenceServiceHealthProbe
```

Transport and infrastructure details depend on stable application abstractions. Controllers do not know Python DTOs, service URLs, `HttpClient` configuration, PyTorch types, registry files, or artifact paths.

## Dependency Injection and Lifetimes

`Program.cs` is the composition root.

- `IImageUploadValidator` is scoped;
- `IAnomalyAnalyzer` uses a typed `HttpClient` with `PythonServiceAnomalyAnalyzer`;
- `IInferenceModelCatalogProvider` uses a typed `HttpClient` with `PythonInferenceModelCatalogProvider`;
- `IInferenceServiceHealthProbe` uses a separate typed `HttpClient`;
- typed clients share validated base address and timeout configuration while using responsibility-specific paths;
- exception handlers, options, controllers, CORS, and OpenAPI are registered centrally.

Separate typed clients allow catalog, prediction, and health behavior to evolve and be tested independently.

## Middleware and Endpoint Pipeline

1. map OpenAPI in Development;
2. handle typed exceptions;
3. generate responses for otherwise empty errors;
4. redirect HTTP to HTTPS;
5. apply CORS;
6. apply authorization middleware;
7. map controllers.

Authentication and authorization policies are not currently implemented.

## Model-Catalog Request Flow

1. a client calls `GET /api/v1/models`;
2. `ModelsController` invokes `IInferenceModelCatalogProvider`;
3. `PythonInferenceModelCatalogProvider` calls the configured internal catalog path;
4. Python returns its enabled models and configured default;
5. the adapter deserializes and validates the catalog;
6. application catalog types enforce consistency;
7. the controller maps entries into public contracts;
8. the client receives the default and available models.

No catalog is cached in the current backend. Each public catalog request reflects a new Python catalog request.

## Analysis Request Flow

1. the client submits multipart `image` and optional `modelId` to `POST /api/v1/analyses`;
2. server and multipart handling apply request limits;
3. ASP.NET Core binds `AnalysisRequest`;
4. `AnalysesController` validates the image;
5. invalid uploads return controlled client errors;
6. the controller normalizes blank model selection to null;
7. it creates `ImageAnalysisInput` with stream, media type, trace ID, and selection;
8. `PythonServiceAnomalyAnalyzer` creates internal multipart content;
9. it adds `modelId` only when selection is non-null;
10. Python resolves the explicit model or its registry default;
11. the selected runtime decodes, preprocesses, scores, decides, and creates a heatmap;
12. the adapter validates the returned result and actual model identity;
13. the controller maps the public response with duration and trace ID.

Raw image bytes are not persisted or logged.

## Failure Flow

### Before Inference

- missing or empty upload: `400`;
- signature mismatch: `400`;
- file too large: `413`;
- unsupported media type: `415`.

### Catalog Retrieval

- Python unreachable or timed out: `503`;
- unsuccessful Python status: `503`;
- malformed or inconsistent catalog: `503`.

### During Prediction

- Python image decode rejection: `400`;
- Python unavailable or timed out: `503`;
- unknown model or other unsuccessful Python status: `503`;
- malformed or inconsistent result: `503`;
- invalid heatmap: `503`.

Caller cancellation propagates through the asynchronous chain.

## Health Model

### Liveness

`GET /health/live` verifies the ASP.NET Core process without contacting Python.

### Readiness

`GET /health/ready` checks the Python health endpoint. It verifies dependency reachability, not catalog validity for every request, prediction quality, or model suitability.

Python is responsible for validating and loading its configured registry and enabled artifacts before it becomes healthy.

## Model Contract Boundary

Python remains authoritative for:

- registry contents, enabled models, and default selection;
- stable runtime resolution;
- artifact metadata and category;
- preprocessing and input size;
- features, memory, scoring, aggregation, threshold, and decision;
- heatmap generation.

The backend validates contract shape and consistency. It does not recalculate inference or localization data. The public response returns the model-generated heatmap, not raw patch scores or a pre-blended overlay.

## Identity and Observability

The backend uses `HttpContext.TraceIdentifier` for analysis identity. It is logged, forwarded through `ImageAnalysisInput` and `X-Correlation-ID`, and returned in success and Problem Details.

Bounded logs include trace ID, validation outcome, content type, file size, actual model ID and category after success, decision, duration, and normalized failure category.

Logs exclude raw images, registry contents, feature memories, artifact paths, secrets, and Python stack traces.

## CORS and OpenAPI Boundaries

CORS uses an explicit origin allowlist. No browser origin is enabled by default. CORS does not provide authentication or authorization.

OpenAPI is exposed only in Development and documents the public catalog, analysis, health-related response shapes, multipart model selection, and heatmap contract. It does not expose internal Python DTOs, registry files, or artifact structure.

## Testing Boundaries

### Unit Tests

Cover:

- upload validation;
- application model-catalog invariants;
- Python catalog mapping and failures;
- Python prediction request construction including optional `modelId`;
- prediction response and heatmap validation;
- health probing;
- exception and domain invariants.

### Integration Tests

Cover:

- liveness and readiness;
- public model-catalog success and failure;
- analysis behavior and model-ID forwarding;
- Problem Details;
- options binding and startup validation including `ModelCatalogPath`;
- dependency registration;
- CORS;
- OpenAPI metadata and schemas.

The ordinary backend suite uses controlled handlers and hosts and requires no dataset, registry, artifact, or live Python process.

### End-to-End Verification

Manual native and Docker workflows verified a four-model catalog and model-specific analyses. Capsule, Bottle, Candle, and Cashew were exercised through the desktop; Capsule and Cashew were additionally selected through the containerized backend. Responses identified the selected model and contained valid heatmaps.

## Security and Trust Boundaries

Uploads are untrusted and receive bounded transport validation. Python is an internal trusted dependency, but its catalog and prediction responses are still validated.

The backend never accepts or reads registry and artifact files. Public responses omit paths, loader details, command lines, secrets, and stack traces.

Production deployment must define network isolation, authentication where needed, TLS termination, rate limiting, and secret handling.

## Deferred Architecture

- dedicated unknown-model public error mapping;
- catalog caching and refresh policy;
- database persistence and history;
- authentication and authorization;
- queues or distributed workers;
- direct .NET inference;
- Python process management by the backend;
- automatic visual category recognition;
- batch inference;
- production camera or PLC integration;
- deployment scaling and telemetry export.

These should be added only after explicit requirements and verification plans exist.

## Related Documentation

- `ProjectSpecification.md` – stable scope and requirements
- `ApiContract.md` – public HTTP contract
- `ModelIntegrationStrategy.md` – Python integration strategy
- `DevelopmentStatus.md` – verified progress
- Model repository: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Last Updated

2026-08-21
