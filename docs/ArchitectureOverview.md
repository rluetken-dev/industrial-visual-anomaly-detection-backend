# Industrial Visual Anomaly Detection Backend – Architecture Overview

## Purpose

This document describes the backend's implemented component boundaries, dependency direction, request flow, and integration points.

It deliberately avoids detailed HTTP schemas and implementation history. Those belong in `ApiContract.md` and `DevelopmentStatus.md`.

## System Context

The backend provides one stable HTTP boundary between client applications and the Python anomaly-inference runtime.

```text
+----------------+       +------------------------------+
| Web client     |------>|                              |
+----------------+       | ASP.NET Core backend         |
                         |                              |
+----------------+       | validation and orchestration |
| Desktop client |------>|                              |
+----------------+       +---------------+--------------+
                                         |
                                         | HTTP multipart request
                                         v
                         +------------------------------+
                         | Python FastAPI service       |
                         |                              |
                         | preprocessing and inference |
                         +---------------+--------------+
                                         |
                                         v
                         +------------------------------+
                         | Exported model artifact      |
                         |                              |
                         | metadata and feature memory |
                         +------------------------------+
```

Clients do not load model artifacts, calculate thresholds, reproduce preprocessing, or implement anomaly-decision rules. They submit an image to the backend and consume a client-neutral response.

## Repository Boundary

The system is intentionally split across separate repositories.

### Backend Repository

The backend owns:

- the public HTTP API;
- transport and image-upload validation;
- request-size limits;
- application-level inference abstractions;
- Python-service communication;
- response and failure mapping;
- liveness and readiness behavior;
- CORS, configuration, OpenAPI, logging, and automated backend tests.

### Model Repository

The Python model repository owns:

- dataset validation and deterministic splits;
- preprocessing and feature extraction;
- feature-memory construction and sampling;
- anomaly scoring and threshold selection;
- model evaluation and heatmap generation;
- artifact export and loading;
- reference inference behavior;
- the internal FastAPI inference service.

The backend does not duplicate Python/PyTorch model logic. The HTTP service contract is the integration boundary between the repositories.

## Repository Layout

The backend repository uses `src` for production code, `tests` for automated tests, `docs` for stable technical documentation, and `scripts` for local verification tooling.

```text
industrial-visual-anomaly-detection-backend/
|-- .github/
|   `-- workflows/
|       `-- ci.yml
|-- docs/
|-- scripts/
|   `-- verify-local-stack.ps1
|-- src/
|   `-- IndustrialVisualAnomalyDetection.Api/
|-- tests/
|   `-- IndustrialVisualAnomalyDetection.Api.Tests/
|-- IndustrialVisualAnomalyDetection.slnx
|-- README.md
`-- COMMITS.md
```

The current implementation remains in one production project because its responsibilities are small and the logical boundaries are already explicit. Additional .NET projects should be introduced only when separation creates a measurable maintenance or deployment benefit.

## Production Project

### `IndustrialVisualAnomalyDetection.Api`

The API project owns application startup, dependency registration, middleware configuration, controllers, public contracts, application abstractions, validation, error handling, and the concrete Python-service adapter.

Its internal directories represent logical boundaries rather than independent deployment units.

```text
IndustrialVisualAnomalyDetection.Api/
|-- Application/
|   |-- Analysis/
|   `-- Health/
|-- Contracts/
|   |-- Analyses/
|   `-- Health/
|-- Controllers/
|-- Errors/
|-- Infrastructure/
|   `-- Inference/
|-- Options/
|-- Validation/
|   `-- Images/
|-- Program.cs
|-- appsettings.json
`-- IndustrialVisualAnomalyDetection.Api.csproj
```

## Logical Components

### HTTP and Controller Layer

Implemented by `AnalysesController`, `HealthController`, and the public request and response contracts.

Responsibilities:

- expose HTTP routes;
- bind multipart image uploads;
- invoke transport-level validation;
- translate application results into stable responses;
- map direct validation failures to appropriate status codes;
- describe endpoint behavior for OpenAPI;
- measure and log request-level analysis duration.

Controllers remain thin. They do not execute model inference, parse artifact metadata, reproduce preprocessing, or calculate anomaly thresholds.

### Validation Layer

Implemented by `IImageUploadValidator` and `ImageUploadValidator`.

Responsibilities:

- require one uploaded image;
- reject empty files;
- enforce the configured file-size limit;
- restrict declared media types to configured PNG and JPEG values;
- inspect file signatures;
- ensure the declared media type agrees with the uploaded content.

Validation before inference is intentionally bounded. Full image decoding remains the responsibility of the Python model runtime, which owns the actual preprocessing implementation.

### Application Analysis Boundary

Implemented by:

- `IAnomalyAnalyzer`;
- `ImageAnalysisInput`;
- `AnomalyAnalysisResult`;
- typed analysis exceptions.

`IAnomalyAnalyzer` is the stable application-level boundary consumed by the controller. It accepts validated image content and returns model identity, category, score, threshold, and decision without exposing HTTP, FastAPI, PyTorch, or artifact-layout details.

`ImageAnalysisInput` and `AnomalyAnalysisResult` enforce their own null and value invariants. This prevents invalid state from silently crossing application boundaries.

### Application Health Boundary

Implemented by `IInferenceServiceHealthProbe`.

The health controller depends on this abstraction rather than directly constructing requests to the Python service. Liveness remains independent from external dependencies, while readiness uses the probe to report whether inference can currently be served.

### Python Inference Adapter

Implemented by `PythonServiceAnomalyAnalyzer`.

Responsibilities:

- send validated image content to the configured Python prediction path;
- use multipart form data with the `image` field;
- forward the current ASP.NET Core trace identifier to Python as `X-Correlation-ID`;
- apply the configured `HttpClient` timeout;
- deserialize and validate the Python response;
- reject missing, malformed, non-finite, negative, or logically inconsistent values;
- convert a valid service response into `AnomalyAnalysisResult`;
- translate transport, timeout, JSON, service-status, and decoded-image failures into typed application exceptions.

The adapter is the only component that knows the Python prediction protocol. Controllers and public response contracts do not depend on its DTOs.

### Python Health Adapter

Implemented by `PythonInferenceServiceHealthProbe`.

Responsibilities:

- call the configured Python health path;
- return ready for successful HTTP responses;
- return not ready for connection and adapter-level timeout failures;
- preserve caller-request cancellation rather than treating it as dependency failure.

The probe is deliberately lightweight and does not execute a model prediction for every readiness request.

### Error Boundary

Implemented through ASP.NET Core Problem Details and dedicated exception handlers.

Responsibilities:

- convert unreadable decoded image content to `400 Bad Request`;
- convert unavailable, invalid, or timed-out inference behavior to `503 Service Unavailable`;
- include the backend trace identifier;
- prevent runtime-specific exceptions, local paths, stack traces, and service internals from entering public responses.

Direct upload-validation failures are mapped by the controller because they are known before the application inference boundary is invoked.

### Configuration Boundary

Strongly typed options represent operational settings.

`ImageUploadOptions` controls:

- maximum image-file size;
- maximum multipart request-body size;
- allowed PNG and JPEG media types.

`PythonInferenceOptions` controls:

- service base URL;
- prediction path;
- health path;
- request timeout.

`ApiCorsOptions` controls:

- the explicit set of browser origins allowed to call the API.

All three option groups are validated during startup. Invalid operational configuration prevents the application from starting instead of causing a delayed request-time failure.

Machine-specific values can be supplied through normal ASP.NET Core configuration sources, including environment variables. Secrets and local artifact paths are not committed.

## Dependency Direction

Dependencies point from transport and infrastructure details toward stable application abstractions.

```text
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

The controllers know the application and validation abstractions. They do not know Python response DTOs, service URLs, `HttpClient` setup, PyTorch types, or artifact files.

Infrastructure adapters implement application interfaces and are registered through dependency injection in `Program.cs`.

## Dependency Injection and Lifetimes

The composition root is `Program.cs`.

- `IImageUploadValidator` is registered as scoped;
- `IAnomalyAnalyzer` is provided by a typed `HttpClient` using `PythonServiceAnomalyAnalyzer`;
- `IInferenceServiceHealthProbe` is provided by a separate typed `HttpClient`;
- each typed client receives the configured base address and timeout;
- exception handlers are registered with ASP.NET Core;
- options are bound and validated on startup;
- controllers and OpenAPI services are registered centrally.

The two typed HTTP clients keep analysis and readiness responsibilities separate while sharing the same validated service configuration.

## Middleware and Endpoint Pipeline

The application pipeline is composed in this order:

1. map the OpenAPI document in the Development environment;
2. handle typed exceptions through the exception-handler pipeline;
3. generate responses for otherwise empty error status codes;
4. redirect HTTP to HTTPS;
5. apply the configured CORS policy;
6. apply authorization middleware;
7. map controller endpoints.

Authentication and authorization policies are not currently implemented. The authorization middleware leaves room for future policy introduction without implying that the current API is access-controlled.

## Analysis Request Flow

The implemented analysis flow is:

1. the client submits one multipart image to `POST /api/v1/analyses`;
2. Kestrel and multipart form handling apply the configured request-body limit;
3. ASP.NET Core binds the form to `AnalysisRequest`;
4. `AnalysesController` invokes `IImageUploadValidator`;
5. invalid size, type, presence, or signature returns a controlled client error;
6. the controller opens the validated upload stream and creates `ImageAnalysisInput`;
7. `IAnomalyAnalyzer` dispatches to `PythonServiceAnomalyAnalyzer`;
8. the adapter posts the image to the Python prediction endpoint;
9. the Python service decodes and preprocesses the image, executes inference, and returns its result;
10. the adapter validates the returned contract and creates `AnomalyAnalysisResult`;
11. the controller maps the result to `AnalysisResponse`;
12. the backend returns the result with processing time and trace identifier.

Raw image bytes are not persisted or logged by the backend.

## Failure Flow

Failures are handled at the boundary where they become known.

### Before Inference

- missing or empty upload: `400 Bad Request`;
- file-signature mismatch or invalid image declaration: `400 Bad Request`;
- file too large: `413 Payload Too Large`;
- unsupported media type: `415 Unsupported Media Type`.

### During Inference

- Python service rejects unreadable decoded image content: `400 Bad Request`;
- Python service unavailable: `503 Service Unavailable`;
- Python request exceeds the configured adapter timeout: `503 Service Unavailable`;
- Python returns malformed or inconsistent JSON: `503 Service Unavailable`;
- Python returns another unsuccessful service status: `503 Service Unavailable`.

Caller cancellation is propagated through the asynchronous call chain and is not deliberately converted into a service-unavailable response.

## Health Model

Health is separated into two concepts.

### Liveness

`GET /health/live` verifies that the ASP.NET Core process is running and can answer requests. It does not contact Python or execute inference.

### Readiness

`GET /health/ready` calls the configured Python health endpoint through `IInferenceServiceHealthProbe`.

- Python health succeeds: backend returns `200 OK` and `ready`;
- Python cannot be reached or times out: backend returns `503 Service Unavailable` and `not_ready`.

Readiness verifies service availability, not prediction quality or dataset validity. The Python service is responsible for loading and validating its configured artifact during its own startup.

## Model Contract Boundary

The Python runtime remains authoritative for:

- expected preprocessing;
- input resolution;
- feature extraction;
- feature-memory interpretation;
- patch-score aggregation;
- threshold selection;
- normal-versus-anomalous decision logic;
- model and category identity.

The backend validates that a received score and threshold are finite and non-negative and that the returned decision is consistent with `score > threshold`. It does not recalculate the model output independently.

The current public backend response does not expose patch maps or heatmaps.

## Request and Correlation Identity

The backend uses `HttpContext.TraceIdentifier` as the analysis request identity.

That value is:

- included in structured backend logs;
- passed through `ImageAnalysisInput`;
- forwarded to the Python service as `X-Correlation-ID`;
- included in successful analysis responses;
- included in mapped Problem Details responses.

The current implementation does not replace `HttpContext.TraceIdentifier` with a client-supplied correlation header. Client-controlled correlation adoption would require an explicit, validated middleware decision.

## Observability Boundary

The backend records bounded operational information:

- trace identifier;
- upload validation outcome;
- declared content type and file size;
- model identity and category after successful inference;
- normal or anomalous decision;
- request-level processing duration;
- normalized failure category through mapped exceptions.

The backend does not log raw images, full model feature memory, local artifact paths, secrets, or Python stack traces.

Production metrics, distributed tracing export, dashboards, and alerting are deferred until a deployment environment exists.

## CORS Boundary

CORS is configured through an explicit allowlist.

- no browser origin is allowed by default;
- configured origins must be absolute HTTP or HTTPS origins without paths, queries, or fragments;
- an allowed origin may use the API methods and headers required by the browser client;
- CORS does not replace authentication, authorization, or network security.

The first web client can supply its development origin through configuration without requiring controller changes.

## OpenAPI Boundary

OpenAPI is exposed only in the Development environment. The document describes the public HTTP transport contract and is protected by integration tests for the analysis operation.

OpenAPI does not describe internal Python DTOs, model artifact files, feature-memory structure, or implementation-specific exceptions.

## Testing Boundaries

The automated test strategy has two ordinary CI levels.

### Unit Tests

Unit tests cover:

- upload validation;
- Python adapter request and response behavior;
- Python health probing;
- exception and invariant enforcement;
- response consistency rules.

### Integration Tests

Integration tests cover:

- liveness and readiness contracts;
- analysis endpoint behavior;
- Problem Details responses;
- dependency registration and options binding;
- startup configuration validation;
- CORS behavior;
- OpenAPI request and response metadata.

The normal backend test suite uses controlled HTTP handlers and test hosts. It does not require MVTec datasets, a feature memory, or a running Python service.

### End-to-End Verification

`scripts/verify-local-stack.ps1` performs manual local verification across the real Python service and backend. An optional image exercises the complete analysis path.

This end-to-end check complements automated CI but is not part of ordinary backend CI because the large external model artifact is intentionally not stored in the repository.

## Security and Trust Boundaries

Uploaded files are untrusted. The backend applies bounded transport validation before forwarding content to Python, and the Python runtime performs actual image decoding.

The Python service is currently treated as an internal trusted dependency, but its response is still validated before use. Public responses never expose local paths, raw loader errors, command lines, secrets, or stack traces.

The current local setup does not provide authentication or transport hardening between backend and Python. Production deployment must define network isolation, service authentication if required, TLS termination, rate limiting, and operational secret handling.

## Deferred Architecture

The current architecture does not require:

- database persistence or analysis history;
- authentication and authorization policies;
- message queues or distributed workers;
- background model training;
- direct .NET model inference;
- Python child-process management by the backend;
- multiple active model versions or category routing;
- heatmap transport through the public contract;
- batch inference;
- production camera or PLC integration;
- container orchestration.

These components should be introduced only after an explicit, verified requirement.

## Remaining Architecture Decisions

- client-side workflow and presentation boundaries;
- whether localization data belongs in a future public response;
- artifact distribution and deployment packaging;
- Docker Compose topology for the complete stack;
- production service-to-service security;
- future persistence, authentication, and authorization requirements;
- performance targets and scaling strategy under deployment-like load.

None of these decisions blocks initial web-client development against the current backend contract.

## Related Documentation

- `ProjectSpecification.md` – stable scope and requirements
- `ApiContract.md` – versioned HTTP contracts
- `ModelIntegrationStrategy.md` – selected runtime-integration approach
- `DevelopmentStatus.md` – verified implementation progress
- Model repository: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Last Updated

2026-08-15
