# Industrial Visual Anomaly Detection Backend – Model Integration Strategy

## Purpose

This document records the selected model-inference boundary, the reasons for that decision, its implemented behavior, and the conditions under which the decision should be revisited.

Verified implementation progress belongs in `DevelopmentStatus.md`, public HTTP behavior in `ApiContract.md`, and component dependency direction in `ArchitectureOverview.md`.

## Decision Summary

**Selected approach: a separately running Python FastAPI inference service called by the ASP.NET Core backend over HTTP.**

The decision is implemented and verified. It replaces the earlier open comparison between direct .NET inference, a Python service, and Python child-process invocation.

The current topology is:

```text
Client
  |
  v
ASP.NET Core backend
  |
  | internal HTTP multipart request
  v
Python FastAPI inference service
  |
  v
Loaded PyTorch model artifact
```

The ASP.NET Core backend remains the public client-facing API. The Python service is an internal model-runtime dependency rather than a second public product API.

## Decision Date and Evidence

The Python-service integration was selected and implemented during the initial backend-model integration milestone in August 2026.

Verified evidence includes:

- the Python service loads a versioned exported artifact during startup;
- Python service liveness is available at `GET /health/live`;
- prediction is available at `POST /api/v1/predictions`;
- the ASP.NET Core backend calls the service through typed `HttpClient` adapters;
- backend readiness reflects Python service availability;
- transport, timeout, malformed response, invalid result, and unreadable image failures are mapped deliberately;
- backend and Python unit and integration tests pass independently;
- a real MVTec AD Capsule `good` image produced the expected normal result;
- a real MVTec AD Capsule `poke` image produced the expected anomalous result;
- path-based and stream-based Python inference produced identical score, threshold, and decision values;
- a real image was successfully analyzed through the complete Python-service-to-backend flow;
- the end-to-end result included model identity, category, score, threshold, decision, processing time, trace identifier, and a Base64-encoded PNG heatmap;
- the heatmap returned through the backend was decoded into a readable `320 × 320` PNG and visually inspected.

The selected approach therefore has stronger implementation and parity evidence than the alternatives for the current model format.

## Starting Point

The separate Python model repository provides:

- deterministic image preprocessing;
- frozen ResNet18 feature extraction;
- patch-embedding construction;
- feature-memory construction and optional sampling;
- nearest-neighbor patch scoring;
- top-fraction image-score aggregation;
- threshold-based anomaly decisions;
- anomaly heatmap generation;
- versioned Python/PyTorch artifact export and loading;
- stream-based and path-based reference inference;
- the internal FastAPI inference service;
- verified normal and anomalous Capsule predictions.

Model repository:

<https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

The current verified Capsule artifact contains:

```text
metadata.json
feature_memory.pt
```

The complete Capsule feature memory is approximately 410 MiB. The artifact remains Python/PyTorch-specific and is not a framework-neutral production package.

## Integration Goals

The selected boundary must:

1. preserve the verified Python inference behavior;
2. keep public clients independent from Python and PyTorch;
3. keep controllers independent from runtime-specific details;
4. accept validated image streams without temporary backend files;
5. return model identity, category, score, threshold, decision, and model-generated heatmap data;
6. support cancellation and bounded adapter execution;
7. expose lightweight dependency health;
8. translate internal failures into stable backend outcomes;
9. avoid loading the large model artifact once per request;
10. remain replaceable behind application abstractions.

The current implementation satisfies these baseline goals.

## Stable Backend Boundary

The backend application depends on `IAnomalyAnalyzer` rather than directly on FastAPI or Python types.

Conceptual input:

```text
ImageAnalysisInput
- readable image stream
- declared content type
- optional backend trace identifier
```

Conceptual output:

```text
AnomalyAnalysisResult
- model identifier
- category
- anomaly score
- decision threshold
- anomalous decision
- PNG anomaly heatmap
```

The boundary deliberately excludes:

- Python response models;
- PyTorch tensors;
- artifact file paths;
- feature-memory structures;
- preprocessing objects;
- HTTP status codes;
- FastAPI exceptions.

This allows the controller and public API contract to remain stable if the concrete inference adapter is replaced later.

## Stable Health Boundary

Backend readiness depends on `IInferenceServiceHealthProbe`.

The abstraction returns whether the required inference dependency is currently ready without exposing connection details or executing an expensive prediction.

`HealthController` depends only on this boundary. The concrete HTTP probe remains infrastructure code.

## Internal Python Service Contract

The Python service is versioned independently as an internal runtime contract.

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

The Python application loads and stores its runtime during FastAPI startup. A successfully started service therefore has a configured runtime available in application state.

### Prediction

```http
POST /api/v1/predictions
Content-Type: multipart/form-data
```

Multipart field:

| Field | Type | Required |
| --- | --- | --- |
| `image` | binary file | yes |

Successful response:

```json
{
  "modelId": "mvtec-ad-capsule-320",
  "category": "capsule",
  "score": 4.992109298706055,
  "threshold": 2.501821517944336,
  "isAnomalous": true,
  "heatmap": {
    "contentType": "image/png",
    "width": 320,
    "height": 320,
    "dataBase64": "<base64-encoded PNG>"
  }
}
```

The Python runtime generates the heatmap from threshold-normalized patch scores and encodes it as an RGB PNG. The internal response requires heatmap media type, positive dimensions, and non-empty Base64 data.

Unreadable decoded image content returns HTTP `400 Bad Request` from Python. Other runtime failures are not exposed directly to public clients; the backend maps them into its own stable error boundary.

The internal response intentionally omits backend-specific processing time and public trace response fields. Those belong to the ASP.NET Core API layer.

## Backend Adapter Behavior

`PythonServiceAnomalyAnalyzer` implements `IAnomalyAnalyzer`.

For each analysis it:

1. receives an `ImageAnalysisInput` from the controller;
2. wraps the supplied stream in `StreamContent`;
3. preserves the declared PNG or JPEG media type;
4. creates multipart form data with the field name `image`;
5. creates a POST request for the configured prediction path;
6. forwards the ASP.NET Core trace identifier as `X-Correlation-ID` when present;
7. sends the request through its typed `HttpClient`;
8. maps Python HTTP `400` to an invalid decoded image failure;
9. rejects other unsuccessful statuses as inference unavailable;
10. deserializes the internal response;
11. validates scalar result values and the required heatmap payload;
12. constructs `AnomalyHeatmap` and `AnomalyAnalysisResult`.

The adapter validates that:

- model ID is non-empty;
- category is non-empty;
- score is finite and non-negative;
- threshold is finite and non-negative;
- `isAnomalous` equals the result of `score > threshold`;
- heatmap is present;
- heatmap content type is `image/png`;
- heatmap width and height are positive;
- heatmap data is non-empty and valid Base64.

This validation treats the Python service as an internal dependency while still preventing corrupt or incompatible service output from crossing into the public contract.

## Backend Health-Probe Behavior

`PythonInferenceServiceHealthProbe` implements `IInferenceServiceHealthProbe`.

It sends a GET request to the configured Python health path and reports:

- ready when the response status is successful;
- not ready when the connection fails;
- not ready when the adapter-level timeout expires;
- caller cancellation to the caller rather than converting it into dependency failure.

The probe does not run model inference, inspect the artifact directory, or load model files from the backend process.

## Runtime Lifecycle

The Python runtime is created once during service startup.

Startup performs:

1. environment configuration loading;
2. artifact loading;
3. artifact metadata validation;
4. feature-memory loading;
5. frozen ResNet18 patch-embedding extractor creation;
6. runtime storage in FastAPI application state.

Inference requests reuse the loaded artifact and feature extractor. Expensive artifact initialization therefore does not occur once per request.

The current Python runtime uses a lock around prediction. This makes use of the reusable extractor and feature memory thread-safe but serializes predictions within one service process. Concurrency and horizontal scaling should be measured before production deployment rather than assumed.

## Configuration

### Python Service Configuration

The Python service reads:

| Environment variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `IVAD_MODEL_ARTIFACT` | yes | none | Path to the exported artifact directory |
| `IVAD_MEMORY_CHUNK_SIZE` | no | `4096` | Number of feature-memory entries processed per nearest-neighbor chunk |

`IVAD_MODEL_ARTIFACT` must identify a loadable artifact directory. `IVAD_MEMORY_CHUNK_SIZE` must be a positive integer.

Example:

```powershell
$env:IVAD_MODEL_ARTIFACT = "$PWD\outputs\model-artifacts\mvtec-ad-capsule-320"
$env:IVAD_MEMORY_CHUNK_SIZE = "4096"
```

### Backend Configuration

The backend `PythonInference` section controls:

| Setting | Default | Purpose |
| --- | --- | --- |
| `BaseUrl` | `http://localhost:8000` | Python service base URL |
| `PredictionPath` | `/api/v1/predictions` | Internal prediction path |
| `HealthPath` | `/health/live` | Internal health path |
| `TimeoutSeconds` | `30` | Analysis and health HTTP-client timeout |

The backend validates these values during startup. URLs must be absolute HTTP or HTTPS locations, paths must be root-relative, and timeout must be positive.

Machine-specific paths and service addresses are operational configuration and must not be hard-coded into application classes.

## Artifact Ownership

The Python service owns artifact loading and compatibility checks. The ASP.NET Core backend never reads `metadata.json`, deserializes `feature_memory.pt`, or assumes the feature-memory storage format.

Artifact metadata currently captures model behavior such as:

- schema version;
- dataset and category;
- backbone;
- input size;
- patch-grid size;
- embedding dimension;
- aggregation method and top fraction;
- threshold;
- memory fraction and sampling seed;
- feature-memory entry count.

These details remain inside the Python runtime unless a specific public API requirement justifies exposing one of them.

The backend receives only the model identifier, category, score, threshold, decision, and generated heatmap required for the client-neutral result. It does not inspect artifact internals or reproduce heatmap generation.

## Artifact Distribution

Model artifacts are not committed to either repository by default.

The verified complete Capsule feature memory is large and derives from MVTec AD data. Dataset and artifact redistribution terms must be reviewed before publishing a ready-made artifact.

Until an approved distribution mechanism exists, a developer must:

1. obtain MVTec AD from its official source under the applicable terms;
2. store the dataset outside the repositories;
3. use the model repository export script;
4. configure the resulting local artifact path for the Python service.

Future options include a separately distributed release asset, a smaller demonstrator artifact, or deployment-managed artifact storage. None is selected yet.

## Failure Translation

The integration separates internal runtime failures from public backend errors.

| Internal condition | Backend result |
| --- | --- |
| Python HTTP `400` for unreadable image | Invalid image, HTTP `400` |
| Python connection failure | Inference unavailable, HTTP `503` |
| Adapter-level timeout | Inference unavailable, HTTP `503` |
| Other unsuccessful Python status | Inference unavailable, HTTP `503` |
| Empty response | Inference unavailable, HTTP `503` |
| Invalid JSON | Inference unavailable, HTTP `503` |
| Missing model ID or category | Inference unavailable, HTTP `503` |
| Negative or non-finite numeric result | Inference unavailable, HTTP `503` |
| Inconsistent decision | Inference unavailable, HTTP `503` |
| Missing heatmap | Inference unavailable, HTTP `503` |
| Invalid heatmap media type or dimensions | Inference unavailable, HTTP `503` |
| Missing or invalid heatmap Base64 data | Inference unavailable, HTTP `503` |
| Caller cancellation | Propagated cancellation |

Public errors do not expose the Python service URL, local artifact path, command line, Python traceback, or raw loader exception.

## Timeout and Cancellation

The typed HTTP clients apply the configured timeout. A timeout that was not caused by caller cancellation is converted to inference unavailable.

The controller passes its `CancellationToken` through the analyzer to `HttpClient`. If the caller cancels the request, cancellation remains distinct from a dependency timeout.

The current public mapping uses `503 Service Unavailable` for inference timeout. It does not use `504 Gateway Timeout`.

## Request Identity

The backend includes its ASP.NET Core trace identifier in `ImageAnalysisInput`. The adapter forwards this value to Python as `X-Correlation-ID`.

This supports cross-process diagnostic correlation while leaving request-identity ownership with the backend.

The current Python route does not include the correlation value in its response. The backend includes its trace identifier in the public analysis response and Problem Details independently.

The current implementation does not adopt a client-supplied correlation header as the backend trace identifier.

## Security Boundary

For the verified local setup, the Python service binds to `127.0.0.1:8000`. This keeps it local to the development machine while the backend remains the intended client-facing boundary.

Production deployment must explicitly decide:

- network isolation between backend and Python;
- whether service-to-service authentication is required;
- TLS termination and certificate management;
- container or process identity;
- artifact access permissions;
- request-size enforcement on both services;
- rate limiting and concurrency bounds;
- logging and retention rules.

CORS on the public backend does not secure the internal Python service.

Raw image content is transferred from the backend to Python because Python owns actual decoding and preprocessing. Neither service persists uploaded images by default.

## Observability

The current integration provides:

- backend trace identifier in backend logs;
- trace identifier forwarded to Python as a request header;
- request-level backend processing duration;
- model identifier and category after successful inference;
- normalized backend failure categories;
- liveness and readiness endpoints;
- local end-to-end verification output.

Future production observability may add distributed trace export, metrics, dashboards, structured Python request logging, and alerting. These are deferred until a deployment environment and measurable operational targets exist.

## Local Startup Sequence

The verified startup order is:

1. prepare or locate a compatible exported model artifact;
2. configure `IVAD_MODEL_ARTIFACT` and optional chunk size;
3. start the Python FastAPI service;
4. confirm Python liveness;
5. start the ASP.NET Core backend;
6. confirm backend liveness and readiness;
7. submit an image through the backend analysis endpoint.

The repository README contains the complete commands. The backend script `scripts/verify-local-stack.ps1` automates the health checks and can optionally run the image analysis.

## Verification Strategy

### Python Repository

Python tests verify:

- service settings;
- runtime creation and reuse;
- service startup;
- liveness;
- multipart prediction;
- unreadable image handling;
- path-versus-stream inference parity;
- threshold-normalized PNG heatmap encoding;
- prediction-response heatmap transport.

### Backend Repository

Backend tests verify:

- typed `HttpClient` request construction;
- trace-header forwarding;
- valid response mapping;
- invalid response rejection;
- timeout and cancellation distinction;
- connection and service-status failures;
- dynamic readiness;
- public error mapping;
- configuration validation;
- public analysis contract;
- internal heatmap-response validation;
- `AnomalyHeatmap` invariants;
- public heatmap response mapping.

### End-to-End Verification

The complete local stack has been tested with a real exported Capsule artifact and real MVTec AD images.

The verified anomalous flow was:

```text
Capsule poke image
    -> ASP.NET Core /api/v1/analyses
    -> Python /api/v1/predictions
    -> mvtec-ad-capsule-320 artifact
	-> anomalous response with PNG heatmap
```

The measured local result included approximately 1.7 seconds of backend processing for the verified anomalous example. This is evidence from one development environment, not a guaranteed latency target.

## Accepted Trade-Offs

The selected approach accepts:

- two runtimes instead of one;
- two processes or services to start and monitor;
- image transfer over an internal HTTP boundary;
- service startup and readiness coordination;
- Python and .NET dependency management;
- a currently large external artifact;
- serialized inference within one Python process;
- additional deployment configuration compared with in-process inference.

These costs are accepted because they preserve the verified Python model behavior and avoid a premature, high-risk reimplementation of preprocessing, feature extraction, artifact loading, and nearest-neighbor scoring in .NET.

## Rejected or Deferred Alternatives

### Direct .NET Inference

Direct .NET inference is not selected for the current baseline.

It would require:

- a compatible portable feature-extractor representation;
- exact .NET preprocessing parity;
- a framework-neutral feature-memory format;
- equivalent nearest-neighbor and aggregation behavior;
- expanded artifact compatibility checks;
- new numerical parity evidence;
- measured memory and startup behavior inside ASP.NET Core.

This work provides little immediate client value while the verified Python implementation already exists.

Direct .NET inference may be reconsidered if deployment simplicity, offline constraints, latency, hardware acceleration, or runtime governance creates a concrete need.

### Controlled Python Child Process

Per-request or backend-managed Python process invocation is not selected.

It would introduce:

- process-start overhead;
- command and path management;
- cleanup and timeout complexity;
- difficult concurrency control;
- machine-specific interpreter dependencies;
- a weaker long-running runtime lifecycle.

A persistent FastAPI service provides a clearer health boundary and reuses the loaded artifact across requests.

## Conditions for Reconsideration

The integration decision should be revisited only when evidence shows a material problem or new requirement, such as:

- measured service-boundary latency is unacceptable;
- throughput requirements exceed the current serialized runtime design;
- deployment policy prohibits Python services;
- an approved portable artifact achieves verified numerical parity;
- edge or offline deployment requires one process;
- hardware acceleration is available only through another runtime;
- security requirements mandate a different trust boundary;
- multi-model routing changes the lifecycle substantially.

Any replacement must preserve the `IAnomalyAnalyzer` application contract or introduce an explicitly versioned migration.

## Future Improvements

Possible later improvements include:

- Docker Compose for repeatable backend and Python startup;
- service-to-service authentication;
- artifact checksum and provenance validation;
- controlled artifact distribution;
- warm-up and richer readiness checks;
- measured worker and process scaling;
- bounded request queues and explicit overload behavior;
- production metrics and tracing;
- additional localization forms such as overlays, masks, regions, or raw patch scores;
- multi-category model routing.

These are not required for the current backend and desktop-client baseline.

## Decision Record

| Item | Decision |
| --- | --- |
| Selected integration | Python FastAPI service over HTTP |
| Public boundary | ASP.NET Core backend |
| Application abstraction | `IAnomalyAnalyzer` |
| Readiness abstraction | `IInferenceServiceHealthProbe` |
| Artifact owner | Python model runtime |
| Image transport | Multipart form-data stream |
| Internal prediction path | `/api/v1/predictions` |
| Internal health path | `/health/live` |
| Default local Python address | `http://localhost:8000` |
| Timeout mapping | Backend HTTP `503` |
| Artifact distribution | Local export required until separately approved |
| Decision status | Implemented and end-to-end verified |
| Localization transport | Base64-encoded RGB PNG heatmap |
| Last reviewed | 2026-08-18 |

## Related Documentation

- `ArchitectureOverview.md` – application and adapter boundaries
- `ApiContract.md` – public HTTP contract
- `ProjectSpecification.md` – stable backend requirements
- `DevelopmentStatus.md` – verified implementation progress
- Model repository: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Last Updated

2026-08-18