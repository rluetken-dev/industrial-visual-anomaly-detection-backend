# Industrial Visual Anomaly Detection Backend – Model Integration Strategy

## Purpose

This document records the selected inference boundary, its implemented catalog and prediction behavior, the reasons for the decision, and conditions for reconsideration.

Verified progress belongs in `DevelopmentStatus.md`, public HTTP behavior in `ApiContract.md`, and dependency direction in `ArchitectureOverview.md`.

## Decision Summary

**Selected approach: a separately running Python FastAPI service called by ASP.NET Core over HTTP for model discovery and inference.**

```text
Client
  |
  | public catalog and analysis API
  v
ASP.NET Core backend
  |
  | internal catalog and prediction HTTP contracts
  v
Python FastAPI inference service
  |
  | validated registry and selected runtime
  v
Multiple loaded PyTorch model artifacts
```

ASP.NET Core remains the public client-facing API. Python is an internal dependency and remains authoritative for the available model set, default selection, artifact loading, preprocessing, scoring, decisions, and heatmaps.

## Compatibility Baseline

The registry-capable model and inference service is published as:

```text
industrial-visual-anomaly-detection-model v0.6.0
```

The backend multi-model implementation is committed in:

```text
af710d1 feat: support selectable inference models
```

The Python release preserves legacy single-artifact configuration while adding registry startup, `GET /api/v1/models`, and optional prediction `modelId`.

## Verified Evidence

- Python validates `models.json` and loads all enabled artifacts during startup;
- Python exposes liveness, catalog, and prediction endpoints;
- the backend uses separate typed `HttpClient` adapters for catalog, prediction, and health;
- the backend exposes a client-neutral public catalog;
- explicit model IDs flow from clients through the backend to Python;
- omitted model IDs preserve Python default selection;
- backend readiness reflects Python health;
- transport, timeout, status, JSON, validation, and cancellation behavior is covered by automated tests;
- the public catalog exposed Capsule, Bottle, Candle, and Cashew;
- native desktop integration selected and analyzed with all four;
- Docker integration explicitly selected Capsule and Cashew without recreating services;
- returned model IDs matched requested IDs;
- results included category, score, threshold, decision, trace, duration, and valid PNG heatmaps.

## Integration Goals

The boundary must:

1. preserve verified Python model behavior;
2. keep public clients independent from Python and PyTorch;
3. expose model discovery without duplicating a catalog in .NET;
4. allow stable per-request model selection;
5. preserve default behavior for compatible clients that omit selection;
6. keep controllers independent from runtime protocols;
7. accept image streams without temporary backend files;
8. return actual model identity and complete analysis data;
9. support cancellation and bounded execution;
10. expose lightweight health;
11. translate internal failures into stable backend outcomes;
12. avoid loading artifacts per request;
13. remain replaceable behind application abstractions.

The current implementation satisfies these goals.

## Stable Backend Boundaries

### Model Catalog

The application depends on `IInferenceModelCatalogProvider`.

Conceptual output:

```text
InferenceModelCatalog
- default model identifier
- ordered available model entries
  - stable identifier
  - display name
  - category
  - input size
  - default state
```

The boundary excludes Python DTOs, registry file paths, artifact directories, and transport status codes.

### Analysis

The application depends on `IAnomalyAnalyzer`.

Conceptual input:

```text
ImageAnalysisInput
- readable image stream
- declared content type
- backend trace identifier
- optional model identifier
```

Conceptual output:

```text
AnomalyAnalysisResult
- actual model identifier
- category
- anomaly score
- threshold
- decision
- PNG heatmap
```

The boundary excludes Python response models, tensors, artifact paths, memory structures, preprocessing objects, and HTTP details.

### Health

`IInferenceServiceHealthProbe` reports dependency readiness without exposing connection details or running an expensive prediction.

## Internal Python Service Contract

### Liveness

```http
GET /health/live
```

Successful response:

```json
{
  "status": "healthy"
}
```

Successful startup implies that the configured model source has been loaded. In registry mode, enabled artifacts are loaded before the application becomes healthy.

### Model Catalog

```http
GET /api/v1/models
```

Conceptual response:

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
    }
  ]
}
```

Python derives category and input size from loaded artifacts and publishes only available models. Legacy single-artifact mode appears as a one-model catalog.

### Prediction

```http
POST /api/v1/predictions
Content-Type: multipart/form-data
```

| Field | Type | Required |
| --- | --- | --- |
| `image` | binary file | yes |
| `modelId` | string | no |

A valid explicit ID selects its runtime. Omission selects the Python registry default. An unknown ID returns an unsuccessful Python response.

Successful response:

```json
{
  "modelId": "mvtec-ad-capsule-320",
  "category": "capsule",
  "score": 4.992109,
  "threshold": 2.501822,
  "isAnomalous": true,
  "heatmap": {
    "contentType": "image/png",
    "width": 320,
    "height": 320,
    "dataBase64": "<base64-encoded PNG>"
  }
}
```

The response contains the runtime actually used. Backend processing time and public trace fields remain ASP.NET Core concerns.

## Backend Catalog Adapter

`PythonInferenceModelCatalogProvider` implements `IInferenceModelCatalogProvider`.

For each request it:

1. creates a GET request for `ModelCatalogPath`;
2. sends it through its typed `HttpClient`;
3. rejects unsuccessful statuses;
4. deserializes the Python catalog DTO;
5. maps entries into application model values;
6. constructs `InferenceModelCatalog`, which validates consistency;
7. maps transport, timeout, JSON, and invalid catalog failures to inference unavailable;
8. preserves caller cancellation.

The backend does not cache the catalog and does not read `models.json` directly.

## Backend Prediction Adapter

`PythonServiceAnomalyAnalyzer` implements `IAnomalyAnalyzer`.

For each analysis it:

1. receives validated `ImageAnalysisInput`;
2. wraps the image stream and preserves its media type;
3. adds multipart `image`;
4. conditionally adds `modelId` when non-null;
5. creates a POST request for `PredictionPath`;
6. forwards `X-Correlation-ID`;
7. sends through its typed `HttpClient`;
8. maps Python image `400` to invalid image;
9. maps other unsuccessful statuses to inference unavailable;
10. deserializes and validates result and heatmap;
11. returns `AnomalyAnalysisResult` with the actual model identity.

Validation requires non-empty model ID and category, finite non-negative score and threshold, consistent decision, PNG heatmap media type, positive dimensions, and valid non-empty Base64 data.

The adapter intentionally does not verify explicit selection against a previously retrieved catalog. Python remains authoritative and avoids backend catalog-staleness races.

## Backend Health Adapter

`PythonInferenceServiceHealthProbe` calls `HealthPath` and reports successful status as ready, connection and dependency timeout failures as not ready, and preserves caller cancellation.

The probe does not retrieve the catalog or execute prediction.

## Runtime Lifecycle

### Registry Mode

Python startup:

1. reads `IVAD_MODEL_REGISTRY`;
2. validates registry schema, identifiers, default, paths, and enabled entries;
3. loads every enabled artifact and validates its metadata and memory;
4. creates one frozen feature extractor and runtime per enabled model;
5. stores `InferenceRuntimeRegistry` in FastAPI application state;
6. reuses loaded runtimes across catalog and prediction requests.

### Legacy Mode

`IVAD_MODEL_ARTIFACT` loads one artifact and runtime and presents it through the same service endpoints as a single-model catalog.

Exactly one model source must be configured.

Each Python runtime uses a prediction lock. Requests for a shared runtime are serialized. Eager multi-model loading increases startup time and resident memory; multiple worker processes duplicate all loaded memories.

## Configuration

### Python Service

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `IVAD_MODEL_REGISTRY` | conditionally | none | Path to `models.json` for multi-model mode |
| `IVAD_MODEL_ARTIFACT` | conditionally | none | Legacy single-artifact directory |
| `IVAD_MEMORY_CHUNK_SIZE` | no | `4096` | Nearest-neighbor chunk size |

Exactly one of the two model-source variables is required. Chunk size must be positive.

### Backend

| Setting | Default | Purpose |
| --- | --- | --- |
| `BaseUrl` | `http://localhost:8000` | Python service base URL |
| `PredictionPath` | `/api/v1/predictions` | Prediction path |
| `ModelCatalogPath` | `/api/v1/models` | Catalog path |
| `HealthPath` | `/health/live` | Health path |
| `TimeoutSeconds` | `30` | Typed-client timeout |

Backend configuration is startup-validated. URLs must be absolute HTTP(S), paths root-relative, and timeout positive.

## Registry and Artifact Ownership

Python owns registry parsing, enabled/default selection, artifact loading, compatibility checks, and runtime construction.

The backend never:

- reads `models.json`;
- resolves artifact directories;
- deserializes feature memories;
- assumes PyTorch storage formats;
- loads model runtimes;
- computes catalog metadata from local files.

The backend consumes only the internal model catalog and prediction contracts.

Registries and artifacts remain external to source repositories. Publishing model-service source does not redistribute dataset-derived artifacts.

## Failure Translation

| Internal condition | Public backend result |
| --- | --- |
| Python image `400` | Invalid image, `400` |
| Python connection failure | Inference unavailable, `503` |
| Dependency timeout | Inference unavailable, `503` |
| Unknown model or other unsuccessful status | Inference unavailable, `503` |
| Empty or invalid JSON | Inference unavailable, `503` |
| Invalid catalog | Inference unavailable, `503` |
| Invalid prediction scalar or identity | Inference unavailable, `503` |
| Invalid heatmap | Inference unavailable, `503` |
| Caller cancellation | Propagated cancellation |

A dedicated public unknown-model error is deferred. Public failures do not expose Python URL, registry path, artifact path, traceback, or loader exception.

## Timeout, Cancellation, and Identity

Typed HTTP clients apply the configured timeout. Dependency timeout becomes `503`; caller cancellation remains distinct.

Analysis requests carry `HttpContext.TraceIdentifier` to Python as `X-Correlation-ID`. The backend includes its trace ID in public success and error responses independently of Python.

Catalog requests do not currently define a separate public correlation field.

## Security Boundary

The Python service is an internal dependency. In local native development it binds to loopback; in Compose it is reached through the internal service network.

Production deployment must define network isolation, service authentication if needed, TLS termination, identities, artifact permissions, request limits, concurrency bounds, rate limits, and logging retention.

The backend validates public uploads. Python performs actual decoding. Neither persists uploaded images by default.

## Observability

Current integration provides:

- backend trace IDs;
- correlation header forwarding for analysis;
- request duration;
- actual model ID and category after success;
- normalized error categories;
- liveness and readiness;
- local native and Docker verification output.

Production metrics, trace export, dashboards, and alerting are deferred.

## Verified Startup Sequences

### Native

1. prepare compatible artifacts and `models.json`;
2. configure `IVAD_MODEL_REGISTRY`;
3. start model-service `v0.6.0`;
4. confirm Python health and catalog;
5. start ASP.NET Core;
6. confirm backend liveness, readiness, and catalog;
7. submit an image with a selected catalog model ID.

### Docker Compose

1. mount artifact root read-only;
2. point Python to the container registry path;
3. start inference and wait for health;
4. start backend with internal Python DNS address;
5. retrieve the backend catalog;
6. submit model-specific analyses;
7. verify returned model identity and heatmap.

## Verification Strategy

### Python Repository

Tests cover registry settings and validation, multi-runtime loading, default and explicit selection, catalog endpoint, prediction `modelId`, legacy compatibility, heatmap encoding, and error behavior.

### Backend Repository

Tests cover catalog domain invariants, provider mapping and failures, configuration validation, public catalog endpoint, model-ID forwarding, prediction mapping, timeouts, cancellation, health, errors, OpenAPI, and heatmaps.

### End-to-End

The four-model catalog was verified across Python, backend, desktop, and Docker. Capsule, Bottle, Candle, and Cashew were exercised natively; Capsule and Cashew were explicitly selected through the containerized backend.

This verifies routing and contracts, not independent quality benchmarks for every model.

## Accepted Trade-Offs

- separate .NET and Python runtimes;
- internal HTTP image transfer;
- startup and readiness coordination;
- three responsibility-specific backend HTTP clients;
- eager loading of all enabled Python models;
- increased memory per enabled artifact;
- serialized prediction per runtime;
- external registry and artifact configuration;
- coordinated contract versioning.

These costs preserve verified Python behavior and avoid duplicating model execution in .NET.

## Rejected or Deferred Alternatives

### Direct .NET Inference

Not selected. It requires portable feature extraction, preprocessing parity, framework-neutral memory, equivalent scoring, compatibility checks, numerical evidence, and new memory/startup measurements.

### Backend-Managed Python Child Process

Not selected. It introduces process startup, command and path management, cleanup, timeout, concurrency, and interpreter dependencies. A persistent service provides clearer lifecycle and health behavior.

### Backend-Owned Registry Parsing

Not selected. Duplicating registry logic in .NET would create two authorities, increase compatibility risk, and allow catalog state to diverge from actual loaded Python runtimes.

### Automatic Category Recognition

Not selected. The current workflow uses explicit stable model IDs or the configured default. Inferring category from image content would require a separately trained and evaluated routing model.

## Conditions for Reconsideration

Revisit the decision only with evidence such as unacceptable measured latency, throughput beyond the runtime design, policy prohibiting Python services, verified portable-artifact parity, one-process edge constraints, different hardware requirements, security-driven trust changes, or unacceptable eager-loading memory.

Any replacement must preserve or explicitly migrate both `IInferenceModelCatalogProvider` and `IAnomalyAnalyzer`.

## Future Improvements

- dedicated unknown-model public mapping;
- artifact checksums and provenance validation;
- controlled artifact distribution;
- catalog caching only if justified and safely invalidated;
- warm-up and richer readiness;
- measured worker and runtime scaling;
- lazy model loading and controlled registry reload in Python;
- bounded queues and overload behavior;
- production metrics and tracing;
- additional localization forms.

Docker Compose and explicit multi-model routing are implemented and are no longer future work.

## Decision Record

| Item | Decision |
| --- | --- |
| Selected integration | Python FastAPI service over HTTP |
| Compatible model service | `v0.6.0` |
| Public boundary | ASP.NET Core backend |
| Catalog abstraction | `IInferenceModelCatalogProvider` |
| Analysis abstraction | `IAnomalyAnalyzer` |
| Readiness abstraction | `IInferenceServiceHealthProbe` |
| Registry and artifact owner | Python runtime |
| Public catalog path | `/api/v1/models` |
| Public analysis path | `/api/v1/analyses` |
| Internal catalog path | `/api/v1/models` |
| Internal prediction path | `/api/v1/predictions` |
| Internal health path | `/health/live` |
| Selection transport | Optional multipart `modelId` |
| Default selection | Python registry default |
| Unknown model mapping | Backend `503` |
| Localization transport | Base64 RGB PNG heatmap |
| Artifact distribution | External local artifacts pending separate approval |
| Decision status | Implemented and multi-model verified |
| Last reviewed | 2026-08-21 |

## Related Documentation

- `ArchitectureOverview.md` – application and adapter boundaries
- `ApiContract.md` – public HTTP contract
- `ProjectSpecification.md` – stable requirements
- `DevelopmentStatus.md` – verified progress
- Model repository: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Last Updated

2026-08-21
