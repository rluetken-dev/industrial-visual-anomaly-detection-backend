# Industrial Visual Anomaly Detection Backend – Project Specification

## Purpose

This document defines the stable scope and core requirements of the Industrial Visual Anomaly Detection Backend.

Implementation progress belongs in `DevelopmentStatus.md`. Concrete HTTP contracts belong in `ApiContract.md`. Architectural details belong in `ArchitectureOverview.md`.

## Product Summary

The project provides a client-neutral ASP.NET Core backend for industrial visual anomaly detection.

The backend will accept inspection images, validate requests, coordinate model inference, and return a consistent result to future web and desktop clients.

```text
Web client ───────┐
                  ├──> ASP.NET Core backend ──> Model inference
Desktop client ──┘
```

The backend does not train the anomaly-detection model. Model development, evaluation, and artifact generation remain responsibilities of the separate Python model repository.

## Current Status

- .NET SDK version: 10
- ASP.NET Core Web API project created
- controller-based API template selected
- solution and source-code structure created
- model integration not yet implemented
- public anomaly-analysis endpoint not yet defined
- web and desktop clients not yet implemented

## Goals

- provide one stable backend contract for different client types;
- keep model-specific execution details behind an application boundary;
- validate uploaded images before inference;
- return understandable anomaly decisions and diagnostics;
- support reproducible model and artifact identification;
- provide automated tests, CI, and clear documentation;
- remain suitable for local CPU-oriented development.

## Non-Goals

The initial backend will not:

- train or fine-tune computer-vision models;
- classify the exact defect type;
- manage dataset downloads or benchmark evaluation;
- perform continuous video or camera-stream inspection;
- integrate directly with production machinery or PLCs;
- make certified production-quality decisions;
- provide authentication, persistence, or multi-tenancy unless added by a later requirement.

## System Boundaries

### Backend Responsibilities

- expose versioned HTTP endpoints;
- validate request structure, uploaded files, and configured limits;
- invoke the selected inference implementation;
- translate model output into a client-neutral response;
- report model identity, score, threshold, and decision;
- handle failures without exposing sensitive internal details;
- provide health and readiness information;
- record useful operational diagnostics.

### Model Repository Responsibilities

- dataset qualification and split manifests;
- preprocessing definition;
- model fitting and evaluation;
- threshold selection;
- artifact generation and validation;
- reference inference behavior;
- model limitations and performance evidence.

### Client Responsibilities

- allow users to select or capture an image;
- submit the image through the backend contract;
- present results and understandable errors;
- avoid duplicating inference or decision logic.

## Functional Requirements

### Application Health

- `FR-001` The backend shall expose a liveness endpoint.
- `FR-002` The backend shall expose model-readiness information once inference is integrated.
- `FR-003` Health responses shall not reveal secrets or sensitive machine details.

### Image Analysis

- `FR-010` The backend shall accept one inspection image per initial analysis request.
- `FR-011` The backend shall reject missing, empty, oversized, or unsupported input.
- `FR-012` The backend shall verify that uploaded content can be decoded as an image before inference.
- `FR-013` The backend shall invoke inference through an abstraction rather than directly from a controller.
- `FR-014` The backend shall return an image-level anomaly score.
- `FR-015` The backend shall return the decision threshold used for the request.
- `FR-016` The backend shall return a normal or anomalous decision.
- `FR-017` The backend shall identify the model or artifact used for inference.
- `FR-018` Localization output may be included when supported by the selected integration.

### Error Handling

- `FR-020` Validation failures shall return consistent client-facing errors.
- `FR-021` Unexpected failures shall not expose stack traces or internal file paths.
- `FR-022` Error responses shall include a trace identifier where available.

### Model Integration

- `FR-030` Model execution shall be replaceable without changing controller behavior.
- `FR-031` Model configuration shall be supplied through configuration rather than hard-coded machine paths.
- `FR-032` The backend shall reject incompatible or unavailable model artifacts during readiness checks.
- `FR-033` The backend shall preserve the model's defined preprocessing, aggregation, and threshold behavior.
- `FR-034` Backend results shall be compared with verified Python reference results before an integration is accepted.

## Initial Analysis Response

The exact HTTP schema will be defined in `ApiContract.md`. The stable conceptual result contains:

```text
model identifier
category
anomaly score
decision threshold
normal or anomalous decision
processing duration
optional localization result
trace identifier
```

## Non-Functional Requirements

### Compatibility

- `NFR-001` The backend shall target .NET 10.
- `NFR-002` The initial development workflow shall support Windows.
- `NFR-003` Automated CI checks shall run on a clean hosted environment.

### Maintainability

- `NFR-010` HTTP concerns and inference concerns shall remain separated.
- `NFR-011` Dependencies shall be supplied through dependency injection.
- `NFR-012` Core behavior shall be covered by automated tests.
- `NFR-013` Public contracts shall be documented and versioned.
- `NFR-014` Nullable reference types and implicit global usings shall remain enabled unless a documented reason requires otherwise.

### Security

- `NFR-020` Uploaded files shall be treated as untrusted input.
- `NFR-021` Request size, file type, and processing time shall be bounded.
- `NFR-022` Secrets and machine-specific paths shall not be committed to source control.
- `NFR-023` Raw inspection images shall not be persisted or logged by default.
- `NFR-024` Model artifacts shall be loaded only from trusted configured locations.

### Observability

- `NFR-030` Logs shall use structured fields where practical.
- `NFR-031` Inference diagnostics shall include duration, outcome, and model identity.
- `NFR-032` Logs shall avoid raw image content and sensitive configuration values.

### Performance

- `NFR-040` The backend shall avoid unnecessary copies of uploaded image data.
- `NFR-041` Inference requests shall support cancellation and bounded execution.
- `NFR-042` Concrete latency and throughput targets shall be defined only after the inference boundary is selected and measured.

## Open Decisions

- run inference directly in .NET using a portable artifact;
- invoke the existing Python inference through a separate service or process boundary;
- representation of localization output;
- initial maximum upload size and supported image formats;
- artifact distribution and deployment approach;
- need for persistence, authentication, and request history in later stages.

Open decisions are not implementation commitments.

## Initial Acceptance Criteria

The first backend milestone is complete when:

- the solution builds without warnings or errors;
- automated tests pass locally and in CI;
- liveness and readiness endpoints are implemented;
- one versioned image-analysis endpoint accepts a valid image;
- invalid uploads return documented validation errors;
- inference is accessed through an application abstraction;
- a fixed normal and anomalous reference image produce results consistent with the verified Python implementation;
- setup and API usage are documented.

## Related Repositories

- Model development: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>
- Backend: <https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend>

## Last Updated

2026-08-14
