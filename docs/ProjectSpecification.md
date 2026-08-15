# Industrial Visual Anomaly Detection Backend – Project Specification

## Purpose

This document defines the stable scope and core requirements of the Industrial Visual Anomaly Detection Backend.

Implementation progress belongs in `DevelopmentStatus.md`. HTTP contracts belong in `ApiContract.md`. Component boundaries belong in `ArchitectureOverview.md`. The selected runtime boundary belongs in `ModelIntegrationStrategy.md`.

## Product Summary

The project provides a client-neutral ASP.NET Core backend for industrial visual anomaly detection.

The backend accepts inspection images, applies bounded upload validation, coordinates inference through an internal Python service, and returns a consistent anomaly-analysis result to web and desktop clients.

```text
Web client -------+
                  |
                  +--> ASP.NET Core backend --> Python service --> Model artifact
                  |
Desktop client ---+
```

The backend does not train the anomaly-detection model or implement PyTorch inference directly. Model development, evaluation, artifact generation, and runtime inference remain responsibilities of the separate Python model repository.

## Current Baseline

The first backend milestone is complete. The verified baseline includes:

- .NET 10 ASP.NET Core API;
- liveness and dependency-aware readiness;
- versioned single-image analysis;
- PNG and JPEG upload validation;
- file and request-size limits;
- Python FastAPI inference integration over HTTP;
- stable success and Problem Details contracts;
- structured logging and trace identifiers;
- validated startup options and configurable CORS;
- Development OpenAPI document;
- automated unit and integration tests;
- GitHub Actions CI;
- documented local setup and end-to-end verification.

The backend is ready to support the first web client. Further work should be driven by verified client, deployment, or operational requirements.

## Goals

- provide one stable backend contract for different client types;
- isolate clients from Python, PyTorch, and artifact details;
- keep model execution behind replaceable application abstractions;
- validate uploads before delegating decoding and inference;
- return understandable decisions and diagnostics;
- report model identity and category;
- distinguish process liveness from inference readiness;
- provide controlled errors without exposing internals;
- support reproducible CPU-oriented local development;
- provide automated tests, CI, OpenAPI, and clear documentation.

## Non-Goals

The current backend does not:

- train or fine-tune models;
- download or redistribute datasets;
- generate model artifacts;
- classify the exact defect type;
- expose heatmaps or localization through the public API;
- process video, camera streams, or batches;
- integrate directly with machinery or PLCs;
- make certified production decisions;
- persist images or analysis history;
- provide authentication, authorization, or multi-tenancy;
- route requests across multiple models;
- manage Python as a child process;
- load PyTorch artifacts inside ASP.NET Core;
- define production service-level guarantees.

These items may become future requirements but are not implied by the baseline.

## System Boundaries

### Backend Responsibilities

- expose versioned application and operational health endpoints;
- bind multipart requests and enforce limits;
- validate image presence, media type, size, and signature;
- invoke inference through an application abstraction;
- communicate with the configured Python service;
- validate internal inference responses;
- translate results and failures into public contracts;
- report model identity, category, score, threshold, decision, duration, and trace ID;
- supply OpenAPI and configurable CORS;
- avoid image persistence and sensitive logging by default.

### Python Model Repository Responsibilities

- dataset qualification and deterministic splits;
- preprocessing and feature extraction;
- feature-memory construction and sampling;
- anomaly scoring and threshold selection;
- model evaluation and heatmap generation;
- artifact export, loading, and validation;
- actual image decoding and inference;
- internal FastAPI service;
- reference inference tests and evidence.

### Client Responsibilities

- select or capture a supported image;
- submit one image through the documented multipart field;
- present results and errors understandably;
- call the backend rather than Python directly;
- avoid duplicating preprocessing or decision logic;
- tolerate new optional version 1 response fields.

### Deployment Responsibilities

- provide .NET and Python runtimes;
- provide a compatible artifact to Python;
- protect artifacts and configuration;
- define backend-to-Python network access;
- configure browser origins;
- provide production TLS, access control, monitoring, and rate limiting when required.

## Functional Requirements

### Application Health

- `FR-001` The backend shall expose `GET /health/live`.
- `FR-002` Liveness shall confirm only that ASP.NET Core can answer requests.
- `FR-003` The backend shall expose `GET /health/ready`.
- `FR-004` Readiness shall reflect lightweight Python service availability.
- `FR-005` Readiness shall not execute a complete prediction.
- `FR-006` Health responses shall not reveal private URLs, paths, secrets, or loader errors.
- `FR-007` An unavailable inference service shall produce HTTP `503` and `not_ready`.

### Image Analysis

- `FR-010` The backend shall expose `POST /api/v1/analyses`.
- `FR-011` The endpoint shall accept one multipart field named `image`.
- `FR-012` Missing and empty images shall be rejected.
- `FR-013` Oversized files and request bodies shall be rejected.
- `FR-014` The baseline shall accept only configured PNG and JPEG media types.
- `FR-015` PNG and JPEG signatures shall be validated.
- `FR-016` A signature that disagrees with the media type shall be rejected.
- `FR-017` Actual image decoding shall occur in the Python runtime.
- `FR-018` An image that cannot be decoded shall return a controlled client error.

### Analysis Result

- `FR-030` A successful response shall identify the model and category.
- `FR-031` Score and threshold shall be finite and non-negative.
- `FR-032` Decision shall be `normal` or `anomalous`.
- `FR-033` Decision shall remain consistent with `score > threshold`.
- `FR-034` Processing time shall be non-negative and expressed in milliseconds.
- `FR-035` A non-empty backend trace identifier shall be returned.
- `FR-036` Clients shall treat the returned decision as authoritative.

### Model Integration

- `FR-040` Controllers shall invoke `IAnomalyAnalyzer` or an equivalent abstraction.
- `FR-041` Controllers shall not depend on Python DTOs, PyTorch types, URLs, or artifact paths.
- `FR-042` The baseline adapter shall call Python over HTTP.
- `FR-043` The adapter shall send the image as multipart form data.
- `FR-044` The adapter shall support caller cancellation and a configured timeout.
- `FR-045` Python location and paths shall come from validated configuration.
- `FR-046` The adapter shall validate identity, category, score, threshold, and decision.
- `FR-047` Invalid inference output shall fail closed.
- `FR-048` The backend shall forward its trace identifier to Python.
- `FR-049` Model execution shall remain replaceable without controller changes.

### Python Runtime

- `FR-060` Python shall load the configured artifact during startup.
- `FR-061` Python shall reuse the loaded artifact and extractor.
- `FR-062` Python shall expose lightweight liveness.
- `FR-063` Python shall expose a versioned single-image prediction endpoint.
- `FR-064` Python shall return model ID, category, score, threshold, and decision.
- `FR-065` Unreadable image content shall produce controlled HTTP `400`.
- `FR-066` Runtime configuration shall use environment configuration rather than committed paths.

### Error Handling

- `FR-070` Known failures shall return controlled responses.
- `FR-071` Analysis errors shall use Problem Details where handled by the application boundary.
- `FR-072` Missing, empty, signature-invalid, or unreadable images shall map to HTTP `400` as documented.
- `FR-073` Oversized uploads shall map to HTTP `413`.
- `FR-074` Unsupported media types shall map to HTTP `415`.
- `FR-075` Inference unavailability, timeout, or invalid output shall map to HTTP `503`.
- `FR-076` Public failures shall not expose stack traces, paths, addresses, secrets, or raw Python errors.
- `FR-077` Problem Details shall include the backend trace ID where handled by the application boundary.
- `FR-078` Caller cancellation shall remain distinguishable from adapter timeout.

### Configuration and Browser Integration

- `FR-080` Upload settings shall use strongly typed configuration.
- `FR-081` Python URL, paths, and timeout shall use strongly typed configuration.
- `FR-082` Browser origins shall use an explicit CORS allowlist.
- `FR-083` No browser origin shall be allowed by default.
- `FR-084` Invalid upload, Python, or CORS configuration shall prevent startup.
- `FR-085` Machine-specific values shall be overridable through normal configuration providers.

### API Description and Local Verification

- `FR-090` OpenAPI shall be exposed in Development.
- `FR-091` OpenAPI shall describe binary multipart field `image`.
- `FR-092` The analysis operation shall have stable metadata and response categories.
- `FR-093` Backend build and tests shall not require datasets or artifacts.
- `FR-094` The two-service startup sequence shall be documented.
- `FR-095` A script shall verify Python liveness, backend liveness, and readiness.
- `FR-096` The script shall optionally submit a real image.

## Public Analysis Result

The stable API version 1 result contains model identifier, model category, anomaly score, decision threshold, normal-or-anomalous decision, backend processing duration in milliseconds, and backend trace identifier.

The exact JSON schema is defined in `ApiContract.md`. Localization, patch maps, masks, heatmaps, and region lists are not part of the current result.

## Non-Functional Requirements

### Compatibility and Maintainability

- `NFR-001` The backend shall target .NET 10.
- `NFR-002` The verified Python workflow shall target Python 3.12.
- `NFR-003` The initial local workflow shall support Windows and PowerShell.
- `NFR-004` Backend CI shall run on a clean hosted environment.
- `NFR-005` Incompatible public contract changes shall be versioned.
- `NFR-010` HTTP, application, validation, and infrastructure concerns shall remain separated.
- `NFR-011` Dependencies shall use dependency injection.
- `NFR-012` Runtime-specific types shall not cross application boundaries.
- `NFR-013` Constructors and domain inputs shall reject invalid state explicitly.
- `NFR-014` Core behavior shall have automated tests.
- `NFR-015` Public contracts shall be documented and tested where practical.
- `NFR-016` Additional projects shall require a clear responsibility or deployment boundary.

### Security and Reliability

- `NFR-020` Uploads shall be treated as untrusted.
- `NFR-021` Request size, file size, type, signature, and adapter duration shall be bounded or validated.
- `NFR-022` Secrets and machine-specific paths shall not be committed.
- `NFR-023` Raw images shall not be persisted or logged by default.
- `NFR-024` Artifacts shall be loaded by Python from trusted configured locations.
- `NFR-025` Internal failures shall be normalized before reaching clients.
- `NFR-026` CORS shall not be treated as authentication.
- `NFR-027` Production exposure shall require a separate security review.
- `NFR-030` Liveness shall remain independent from Python.
- `NFR-031` Readiness shall reflect Python availability.
- `NFR-032` Invalid Python output shall fail closed.
- `NFR-033` Invalid configuration shall fail fast.
- `NFR-034` Artifact loading shall occur during Python startup, not per request.
- `NFR-035` Backend tests shall not depend on external services or large artifacts.

### Observability, Performance, and Reproducibility

- `NFR-040` Logs shall use structured fields where practical.
- `NFR-041` Successful diagnostics shall include trace ID, duration, outcome, and model identity.
- `NFR-042` Logs shall avoid image content, artifact paths, and sensitive configuration.
- `NFR-043` The backend trace ID shall be forwarded to Python.
- `NFR-050` The backend shall stream uploads without temporary image files.
- `NFR-051` Inference shall support cancellation and bounded execution.
- `NFR-052` Python shall reuse loaded model resources.
- `NFR-053` Performance targets shall be based on measured deployment needs.
- `NFR-054` Development measurements shall not be represented as guarantees.
- `NFR-055` Setup, build, test, run, and verification commands shall be documented.
- `NFR-056` Artifacts and datasets shall remain outside ordinary Git history.
- `NFR-057` CI shall not require restricted or large external data.

## Configuration Baseline

### Image Upload

| Setting | Checked-in default |
| --- | ---: |
| Maximum file size | 10 MiB |
| Maximum multipart request body | 11 MiB |
| Allowed media types | `image/png`, `image/jpeg` |

### Python Inference

| Setting | Checked-in local default |
| --- | --- |
| Base URL | `http://localhost:8000` |
| Prediction path | `/api/v1/predictions` |
| Health path | `/health/live` |
| Timeout | 30 seconds |

The CORS origin list is empty by default. A browser origin must be configured explicitly. These defaults support local development and are not production recommendations.

## Model Baseline

The verified end-to-end baseline uses:

- MVTec AD Capsule;
- input size 320 × 320;
- patch grid 40 × 40;
- embedding dimension 384;
- top 1 percent patch-score mean aggregation;
- artifact identifier `mvtec-ad-capsule-320`;
- complete feature memory of approximately 410 MiB.

The artifact contains `metadata.json` and `feature_memory.pt` and remains Python/PyTorch-specific. Another artifact may remain API-compatible when identity stays visible and response semantics do not change.

## Artifact and Dataset Policy

- datasets remain outside the repositories;
- generated artifacts are excluded from Git;
- backend CI does not require datasets or artifacts;
- artifact paths are operational configuration;
- redistribution terms must be reviewed before publication;
- until distribution is approved, developers export artifacts locally from their own permitted dataset copy.

## Acceptance Criteria

The first backend milestone is accepted because:

- Debug and Release builds succeed;
- tests pass locally and in CI;
- liveness and dependency-aware readiness are implemented;
- the versioned endpoint accepts a valid image;
- invalid uploads return documented errors;
- limits are enforced;
- inference uses application abstractions;
- the Python adapter is implemented and tested;
- service output is validated;
- normal and anomalous reference cases agree with Python;
- a real image passed through the complete stack;
- OpenAPI describes the upload contract;
- setup and verification are documented.

The backend is ready for initial web-client work. The client must configure its browser origin, submit PNG or JPEG images, and map all documented outcomes.

## Deferred Requirements

- localization or heatmap responses;
- caller-selectable model or category;
- approved artifact distribution;
- Docker and Docker Compose;
- production service security;
- authentication and authorization;
- persistence and analysis history;
- batch analysis and asynchronous jobs;
- rate limiting and overload behavior;
- production monitoring and alerting;
- performance targets and scaling;
- camera, PLC, or device integration;
- direct .NET inference.

Deferred decisions are not commitments and do not block the first web client.

## Change Control

- New behavior shall be justified by a client, deployment, security, or operational requirement.
- Stable API version 1 fields shall not be removed or reinterpreted without migration.
- A runtime replacement shall preserve application and public contracts or document a breaking change.
- Documentation shall distinguish implementation from plans.
- Development measurements shall not become guarantees without deployment-like evidence.

## Related Repositories

- Model and Python inference: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>
- Backend: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend>

## Related Documentation

- `ArchitectureOverview.md` – implemented component boundaries
- `ApiContract.md` – public HTTP contract
- `ModelIntegrationStrategy.md` – selected Python-service integration
- `DevelopmentStatus.md` – verified implementation progress

## Last Updated

2026-08-15
