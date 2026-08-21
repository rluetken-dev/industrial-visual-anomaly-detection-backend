# Industrial Visual Anomaly Detection Backend – Project Specification

## Purpose

This document defines the stable scope and core requirements of the Industrial Visual Anomaly Detection Backend.

Implementation progress belongs in `DevelopmentStatus.md`. HTTP contracts belong in `ApiContract.md`. Component boundaries belong in `ArchitectureOverview.md`. Runtime integration belongs in `ModelIntegrationStrategy.md`.

## Product Summary

The project provides a client-neutral ASP.NET Core backend for industrial visual anomaly detection.

The backend retrieves the available inference-model catalog, accepts inspection images with optional model selection, applies bounded upload validation, coordinates inference and heatmap generation through an internal Python service, and returns a stable analysis result.

```text
Desktop / future web client
        |
        | public catalog + analysis API
        v
ASP.NET Core backend
        |
        | internal catalog + prediction API
        v
Python inference service
        |
        v
Model registry and selected artifact runtime
```

The backend does not train models, parse `models.json`, load artifacts, or execute PyTorch. Those responsibilities remain in the Python model repository.

## Goals

- provide one public contract for different client types;
- isolate clients from Python, PyTorch, registry, and artifact details;
- expose the available model catalog without duplicating it;
- support stable explicit model selection and default fallback;
- keep execution behind replaceable application abstractions;
- validate uploads before Python decoding and inference;
- return decisions, diagnostics, actual model identity, and heatmaps;
- distinguish liveness from dependency readiness;
- normalize failures without exposing internals;
- support reproducible native and containerized local development;
- provide tests, CI, OpenAPI, and documentation.

## Non-Goals

The backend does not:

- train or fine-tune models;
- download or redistribute datasets or artifacts;
- parse or modify model registries;
- load PyTorch artifacts;
- infer the correct product category automatically from image content;
- classify the exact defect type;
- expose raw patch scores, masks, regions, boxes, or pre-blended overlays;
- process video, camera streams, or batches;
- integrate directly with machinery or PLCs;
- make certified production decisions;
- persist images or analysis history;
- provide authentication, authorization, or multi-tenancy;
- manage Python as a child process;
- define production service-level guarantees.

## System Boundaries

### Backend Responsibilities

- expose versioned catalog and analysis endpoints and operational health;
- bind multipart requests and enforce limits;
- validate image presence, media type, size, and signature;
- normalize optional model selection;
- retrieve and validate the Python model catalog;
- forward explicit model selection to Python;
- validate prediction responses;
- map results and failures into public contracts;
- report actual model identity, category, score, threshold, decision, duration, trace ID, and PNG heatmap;
- provide OpenAPI, CORS, configuration, logging, and tests;
- avoid image persistence and sensitive logging.

### Python Responsibilities

- dataset qualification, fitting, evaluation, and artifacts;
- registry schema, enabled entries, and default selection;
- artifact and runtime loading;
- actual image decoding and preprocessing;
- features, scoring, thresholds, decisions, and heatmaps;
- internal liveness, catalog, and prediction endpoints.

### Client Responsibilities

- retrieve the model catalog from the backend;
- select the default or another available model;
- submit one supported image and optional stable model ID;
- present results and errors;
- call the backend rather than Python;
- avoid duplicating preprocessing and decision logic;
- tolerate additive version-1 fields;
- record the actual model returned by analysis;
- decode and display the PNG heatmap when required.

### Deployment Responsibilities

- provide .NET and Python runtimes;
- provide a compatible registry and artifacts to Python;
- protect registry, artifacts, and configuration;
- define backend-to-Python network access;
- configure browser origins;
- provide production TLS, access control, monitoring, and rate limiting when required.

## Functional Requirements

### Health

- `FR-HLT-001` The backend shall expose `GET /health/live`.
- `FR-HLT-002` Liveness shall confirm only that ASP.NET Core answers.
- `FR-HLT-003` The backend shall expose `GET /health/ready`.
- `FR-HLT-004` Readiness shall reflect lightweight Python availability.
- `FR-HLT-005` Readiness shall not execute prediction.
- `FR-HLT-006` Health shall not reveal private URLs, paths, secrets, registry contents, or loader errors.
- `FR-HLT-007` Unavailable inference shall produce `503` and `not_ready`.

### Model Catalog

- `FR-MDL-001` The backend shall expose `GET /api/v1/models`.
- `FR-MDL-002` The endpoint shall return a non-empty available-model collection.
- `FR-MDL-003` The response shall identify one default model.
- `FR-MDL-004` Each entry shall contain ID, display name, category, input size, and default state.
- `FR-MDL-005` Model IDs shall be unique and non-empty.
- `FR-MDL-006` Display names and categories shall be non-empty.
- `FR-MDL-007` Input sizes shall be positive.
- `FR-MDL-008` Exactly one entry shall match the default ID and be marked default.
- `FR-MDL-009` Catalog order shall preserve Python order.
- `FR-MDL-010` The backend shall retrieve the catalog through an application abstraction.
- `FR-MDL-011` The backend shall not maintain a hard-coded model list.
- `FR-MDL-012` Unavailable or invalid Python catalog behavior shall fail closed as `503`.

### Image Analysis

- `FR-ANL-001` The backend shall expose `POST /api/v1/analyses`.
- `FR-ANL-002` The endpoint shall accept multipart `image`.
- `FR-ANL-003` The endpoint shall accept optional multipart `modelId`.
- `FR-ANL-004` Missing and empty images shall be rejected.
- `FR-ANL-005` Oversized files and request bodies shall be rejected.
- `FR-ANL-006` Only configured PNG and JPEG media types shall be accepted.
- `FR-ANL-007` PNG and JPEG signatures shall be validated.
- `FR-ANL-008` Signature and media type shall agree.
- `FR-ANL-009` Python shall perform actual image decoding.
- `FR-ANL-010` Unreadable decoded images shall produce a controlled client error.
- `FR-ANL-011` Null, empty, or whitespace model selection shall be treated as unspecified.
- `FR-ANL-012` A non-empty model ID shall be forwarded unchanged to Python.
- `FR-ANL-013` Omitted selection shall allow Python default selection.

### Analysis Result

- `FR-RES-001` Success shall identify the actual model and category.
- `FR-RES-002` Score and threshold shall be finite and non-negative.
- `FR-RES-003` Decision shall be `normal` or `anomalous`.
- `FR-RES-004` Decision shall be consistent with `score > threshold`.
- `FR-RES-005` Duration shall be non-negative milliseconds.
- `FR-RES-006` A non-empty backend trace ID shall be returned.
- `FR-RES-007` Clients shall treat returned decision as authoritative.
- `FR-RES-008` Success shall include one model-generated heatmap.
- `FR-RES-009` Heatmap media type shall be `image/png`.
- `FR-RES-010` Heatmap dimensions shall be positive.
- `FR-RES-011` Heatmap data shall be non-empty valid Base64.
- `FR-RES-012` Returned model identity shall be authoritative even when explicit selection was supplied.

### Python Integration

- `FR-INT-001` Controllers shall use catalog, analysis, and health abstractions.
- `FR-INT-002` Controllers shall not depend on Python DTOs, PyTorch types, URLs, registry paths, or artifact paths.
- `FR-INT-003` Baseline adapters shall call Python over HTTP.
- `FR-INT-004` Prediction shall send multipart image content.
- `FR-INT-005` Prediction shall add `modelId` only when explicitly selected.
- `FR-INT-006` Adapters shall support cancellation and configured timeout.
- `FR-INT-007` Python location and paths shall use validated configuration.
- `FR-INT-008` Catalog and prediction outputs shall be validated before public mapping.
- `FR-INT-009` Invalid internal output shall fail closed.
- `FR-INT-010` Analysis trace ID shall be forwarded through `X-Correlation-ID`.
- `FR-INT-011` Runtime adapters shall remain replaceable without controller changes.
- `FR-INT-012` The backend shall not parse the Python registry or artifacts.

### Python Compatibility

- `FR-PYT-001` Python shall expose lightweight liveness.
- `FR-PYT-002` Python shall expose a versioned model catalog.
- `FR-PYT-003` Python shall expose a versioned single-image prediction endpoint.
- `FR-PYT-004` Python shall load configured runtime state during startup.
- `FR-PYT-005` Python shall reuse loaded runtimes.
- `FR-PYT-006` Python shall resolve explicit model IDs and a configured default.
- `FR-PYT-007` Prediction shall return actual model ID, category, score, threshold, decision, and heatmap.
- `FR-PYT-008` Unreadable image content shall produce internal `400`.
- `FR-PYT-009` Runtime configuration shall use environment configuration rather than committed paths.
- `FR-PYT-010` Registry and legacy single-artifact modes may remain compatible behind the same HTTP endpoints.

### Error Handling

- `FR-ERR-001` Known failures shall return controlled responses.
- `FR-ERR-002` Application-boundary errors shall use Problem Details.
- `FR-ERR-003` Invalid images shall map to documented `400` outcomes.
- `FR-ERR-004` Oversized uploads shall map to `413`.
- `FR-ERR-005` Unsupported types shall map to `415`.
- `FR-ERR-006` Inference or catalog unavailability, timeout, unsuccessful status, or invalid output shall map to `503`.
- `FR-ERR-007` Unknown Python model responses may map to `503` until a dedicated public contract is introduced.
- `FR-ERR-008` Public failures shall not expose stack traces, paths, addresses, secrets, registries, artifacts, or raw Python errors.
- `FR-ERR-009` Problem Details shall include backend trace ID where handled by the application boundary.
- `FR-ERR-010` Caller cancellation shall remain distinguishable from dependency timeout.

### Configuration and Browser Integration

- `FR-CFG-001` Upload, Python, and CORS settings shall use strongly typed configuration.
- `FR-CFG-002` Python configuration shall include base URL, prediction path, catalog path, health path, and timeout.
- `FR-CFG-003` Python endpoint paths shall be root-relative.
- `FR-CFG-004` Browser origins shall use an explicit allowlist.
- `FR-CFG-005` No browser origin shall be allowed by default.
- `FR-CFG-006` Invalid configuration shall prevent startup.
- `FR-CFG-007` Machine-specific values shall be overridable through normal providers.

### API Description and Verification

- `FR-VRF-001` OpenAPI shall be exposed in Development.
- `FR-VRF-002` OpenAPI shall describe the model catalog.
- `FR-VRF-003` OpenAPI shall describe binary `image` and optional `modelId`.
- `FR-VRF-004` OpenAPI shall describe analysis and heatmap responses.
- `FR-VRF-005` Backend build and tests shall not require datasets, registries, or artifacts.
- `FR-VRF-006` Native and Compose startup sequences shall be documented.
- `FR-VRF-007` Verification shall cover health, readiness, catalog, explicit selection, response identity, and heatmap decoding.

## Non-Functional Requirements

### Compatibility and Maintainability

- `NFR-MNT-001` The backend shall target .NET 10.
- `NFR-MNT-002` The compatible Python baseline shall support Python 3.12.
- `NFR-MNT-003` Initial local workflows shall support Windows and PowerShell.
- `NFR-MNT-004` Backend CI shall run cleanly without external model data.
- `NFR-MNT-005` Breaking public contract changes shall be versioned.
- `NFR-MNT-006` HTTP, application, validation, and infrastructure concerns shall remain separated.
- `NFR-MNT-007` Dependencies shall use dependency injection.
- `NFR-MNT-008` Runtime-specific types shall not cross application boundaries.
- `NFR-MNT-009` Constructors and domain inputs shall reject invalid state.
- `NFR-MNT-010` Core catalog and analysis behavior shall have automated tests.

### Security and Reliability

- `NFR-SEC-001` Uploads shall be untrusted.
- `NFR-SEC-002` Request size, file size, type, signature, and adapter duration shall be bounded or validated.
- `NFR-SEC-003` Secrets and machine-specific paths shall not be committed.
- `NFR-SEC-004` Raw images shall not be persisted or logged by default.
- `NFR-SEC-005` Registry and artifacts shall be controlled by Python deployment, not public backend input.
- `NFR-SEC-006` Internal failures shall be normalized.
- `NFR-SEC-007` CORS shall not be treated as authentication.
- `NFR-SEC-008` Production exposure shall require security review.
- `NFR-REL-001` Liveness shall remain independent from Python.
- `NFR-REL-002` Readiness shall reflect Python health.
- `NFR-REL-003` Invalid Python catalog or prediction output shall fail closed.
- `NFR-REL-004` Invalid configuration shall fail fast.
- `NFR-REL-005` Runtime loading shall occur during Python startup, not per backend request.
- `NFR-REL-006` Missing or invalid heatmap output shall fail closed.

### Observability, Performance, and Reproducibility

- `NFR-OBS-001` Logs shall use structured fields where practical.
- `NFR-OBS-002` Successful analysis diagnostics shall include trace, duration, outcome, and actual model.
- `NFR-OBS-003` Logs shall avoid images, registry content, artifact paths, and sensitive configuration.
- `NFR-OBS-004` Analysis trace ID shall be forwarded to Python.
- `NFR-PER-001` Uploads shall stream without temporary backend files.
- `NFR-PER-002` Integration shall support cancellation and bounded execution.
- `NFR-PER-003` Python shall reuse loaded resources.
- `NFR-PER-004` Performance targets shall use measured deployment needs.
- `NFR-REP-001` Setup, build, test, run, and verification commands shall be documented.
- `NFR-REP-002` Registries, artifacts, and datasets shall remain outside backend Git history.
- `NFR-REP-003` CI shall not require restricted or large external data.

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
| Model catalog path | `/api/v1/models` |
| Health path | `/health/live` |
| Timeout | 30 seconds |

The CORS list is empty by default. These are local-development defaults, not production recommendations.

## Model Compatibility Baseline

The first registry-capable compatible Python release is model/inference service `v0.6.0`.

The verified deployment catalog contained:

```text
mvtec-ad-capsule-320
mvtec-ad-bottle-generalized-320
visa-candle-generalized-q95-320
visa-cashew-generalized-q95-320
```

This is integration evidence, not a guarantee of independent benchmark quality for every model. Future compatible models may be added without changing API version 1 when catalog and analysis semantics remain stable.

## Artifact and Dataset Policy

- datasets, registries, and artifacts remain outside the backend repository;
- backend CI requires none of them;
- Python deployment owns their paths and permissions;
- redistribution terms require separate review;
- publishing backend source does not publish model artifacts.

## Acceptance Criteria

The selectable-model backend milestone is accepted when:

- Debug and Release builds succeed;
- complete automated tests pass locally and in CI;
- liveness and dependency-aware readiness work;
- the public catalog maps Python's default and available models;
- invalid catalogs fail closed;
- the analysis endpoint accepts a valid image and optional model ID;
- explicit model selection is forwarded to Python;
- omitted selection preserves default behavior;
- invalid uploads return documented errors;
- service output and heatmaps are validated;
- the response identifies the model actually used;
- at least two distinct model IDs succeed without service recreation;
- native desktop integration retrieves and selects catalog entries;
- Compose integration loads registry and artifacts read-only;
- a model-specific response heatmap decodes successfully;
- OpenAPI and documentation reflect catalog and selection behavior.

## Deferred Requirements

- dedicated public unknown-model error mapping;
- additional localization forms;
- automatic visual category recognition;
- approved artifact distribution;
- production service security;
- authentication and authorization;
- persistence and history;
- batch analysis and asynchronous jobs;
- catalog caching;
- rate limiting and overload behavior;
- production monitoring and alerting;
- performance targets and scaling;
- camera, PLC, or device integration;
- direct .NET inference.

Caller-selectable models and Docker Compose integration are implemented and are no longer deferred.

## Change Control

- New behavior shall be justified by a client, deployment, security, or operational requirement.
- Stable API version-1 fields shall not be removed or reinterpreted without migration.
- Catalog entries and defaults may change without a new API version while field semantics remain stable.
- Runtime replacement shall preserve application and public contracts or document a breaking change.
- Documentation shall distinguish implementation from plans.
- Development measurements shall not become guarantees without deployment-like evidence.

## Related Repositories

- Model and Python inference: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>
- Backend: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend>
- Docker stack: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-stack>

## Related Documentation

- `ArchitectureOverview.md` – component boundaries
- `ApiContract.md` – public HTTP contract
- `ModelIntegrationStrategy.md` – Python integration
- `DevelopmentStatus.md` – verified progress

## Last Updated

2026-08-21
