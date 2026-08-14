# Industrial Visual Anomaly Detection Backend – Architecture Overview

## Purpose

This document describes the backend's intended component boundaries, dependency direction, request flow, and integration points.

It deliberately avoids detailed HTTP schemas and implementation history. Those belong in `ApiContract.md` and `DevelopmentStatus.md`.

## System Context

The backend provides one application boundary between future clients and anomaly-model inference.

```text
┌────────────────┐       ┌──────────────────────────────┐
│ Web client     │──────>│                              │
└────────────────┘       │ ASP.NET Core backend         │
                         │                              │
┌────────────────┐       │ validation and orchestration │
│ Desktop client │──────>│                              │
└────────────────┘       └──────────────┬───────────────┘
                                        │
                                        ▼
                         ┌──────────────────────────────┐
                         │ Model inference boundary     │
                         │                              │
                         │ .NET runtime or Python       │
                         │ integration – not selected   │
                         └──────────────────────────────┘
```

Clients do not load model artifacts, calculate thresholds, or implement anomaly-decision rules.

## Repository Layout

The repository uses `src` for production projects and `tests` for automated test projects.

```text
industrial-visual-anomaly-detection-backend/
├── src/
│   └── IndustrialVisualAnomalyDetection.Api/
├── tests/
├── docs/
├── IndustrialVisualAnomalyDetection.slnx
├── README.md
└── COMMITS.md
```

Additional projects shall be added only when a clear responsibility requires them. Empty architectural layers are not created merely to match a template.

## Current Project

### `IndustrialVisualAnomalyDetection.Api`

The API project currently owns:

- application startup and dependency registration;
- ASP.NET Core middleware configuration;
- HTTP routing and controllers;
- configuration binding;
- future health and API-documentation endpoints.

Controllers shall remain thin. They translate HTTP input into application calls and translate results into HTTP responses. They shall not contain model execution, image processing, threshold logic, or artifact loading.

## Planned Logical Components

The following are logical responsibilities. They do not all require separate .NET projects immediately.

### HTTP Layer

Responsibilities:

- route requests;
- bind uploads and request data;
- apply transport-level validation;
- map application results to status codes and response models;
- expose versioned contracts.

### Application Layer

Responsibilities:

- coordinate an image-analysis use case;
- enforce application-level request rules;
- invoke the configured inference abstraction;
- construct a client-neutral result;
- support cancellation and timing.

### Inference Abstraction

Responsibilities:

- accept validated image content and request context;
- invoke one concrete model runtime;
- return score, threshold, decision, model identity, and optional localization data;
- hide runtime-specific implementation details from controllers and clients.

Conceptual interface:

```text
Analyze image
    input: validated image content and cancellation
    output: model result or typed failure
```

The exact C# interface will be introduced only when the first use case is implemented.

### Inference Adapter

A concrete adapter will implement the inference abstraction. The selected approach remains open:

- direct .NET inference using a compatible portable artifact;
- communication with a Python inference service;
- controlled invocation of a Python process for an early local integration.

Only the adapter shall depend on runtime-specific model libraries, process handling, or service clients.

### Configuration

Configuration will describe operational choices such as:

- inference adapter selection;
- trusted artifact or service location;
- upload size limit;
- permitted image formats;
- request timeout;
- diagnostic options.

Machine paths and secrets must not be hard-coded or committed.

## Dependency Direction

Dependencies point inward toward stable application abstractions:

```text
HTTP/controller code
        │
        ▼
Application use case
        │
        ▼
Inference abstraction
        ▲
        │
Concrete inference adapter
```

The application may depend on the inference abstraction. A concrete adapter implements that abstraction. The application must not depend directly on a Python process, ONNX Runtime, file-system artifact layout, or external HTTP service.

## Initial Request Flow

The intended analysis flow is:

1. the client submits one image;
2. ASP.NET Core applies request and size limits;
3. the HTTP layer validates required upload metadata;
4. the application validates supported image content;
5. the application invokes the configured inference abstraction;
6. the adapter executes the model runtime;
7. the model result is converted into a client-neutral response;
8. the backend records bounded diagnostics;
9. the response is returned to the client.

Failures are returned through a consistent error contract rather than runtime-specific exceptions.

## Health Model

Health is separated into two concepts:

- **Liveness:** the ASP.NET Core process is running and can answer requests.
- **Readiness:** required model configuration and inference dependencies are available.

Liveness must not depend on a costly inference call. Readiness may verify lightweight artifact or service availability without exposing sensitive details.

## Model Contract Boundary

The backend must preserve the behavior defined by the selected model artifact or inference service:

- expected input preprocessing;
- image resolution;
- category and model identity;
- patch-grid interpretation when localization is available;
- image-score aggregation;
- decision threshold;
- normal-versus-anomalous comparison rule.

The model repository remains the authoritative source for model-development evidence. The backend documents only the runtime contract it consumes.

## Error Boundary

Runtime-specific failures shall be translated into stable application failure categories, for example:

- invalid image;
- unsupported media type;
- request too large;
- model unavailable;
- incompatible artifact;
- inference timeout;
- inference failure.

Public responses shall not include stack traces, local paths, command lines, secrets, or raw model-loader errors.

## Observability Boundary

The backend may record:

- trace identifier;
- endpoint and outcome;
- request and inference durations;
- model identity;
- normalized failure category.

It shall not log raw images or full sensitive configuration by default.

## Testing Boundaries

The planned test strategy has three levels:

1. unit tests for validation, mapping, and application behavior;
2. integration tests for HTTP contracts and dependency registration;
3. parity tests comparing a concrete adapter with fixed Python reference results.

Large benchmark datasets and feature memories shall not be required for ordinary CI. Test fixtures must be small, synthetic, self-created, or otherwise safe to version.

## Deferred Architecture

The initial architecture does not require:

- database persistence;
- authentication or authorization;
- message queues;
- distributed processing;
- background model training;
- multiple deployed model versions;
- production camera or PLC integration.

These components shall be introduced only after an explicit requirement and boundary decision.

## Open Decisions

- final inference runtime and adapter;
- whether application and infrastructure responsibilities need separate projects;
- localization response representation;
- artifact deployment and integrity validation;
- model-readiness verification depth;
- future persistence and authentication requirements.

## Related Documentation

- `ProjectSpecification.md` – stable scope and requirements
- `ApiContract.md` – versioned HTTP contracts
- `DevelopmentStatus.md` – verified implementation progress
- Model repository: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Last Updated

2026-08-14
