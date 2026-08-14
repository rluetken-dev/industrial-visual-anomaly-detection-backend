# Industrial Visual Anomaly Detection Backend – API Contract

## Purpose

This document defines the public HTTP contract of the backend.

Only stable or deliberately selected contract elements belong here. Implementation progress belongs in `DevelopmentStatus.md`.

## Contract Status

**Draft – no custom endpoint is implemented yet.**

The paths and conceptual response fields below define the intended first API version. Concrete examples shall be verified against the running API before the contract is marked implemented.

## General Conventions

- API routes use lowercase path segments.
- Application endpoints are versioned under `/api/v1`.
- Health endpoints are operational endpoints and are not placed under the application API version.
- JSON property names use `camelCase`.
- Timestamps, when added, use UTC and ISO 8601 formatting.
- Durations are represented explicitly in milliseconds.
- Unknown JSON response properties should be ignored by clients for forward compatibility.
- Clients shall use the HTTP status code as the primary transport outcome.

## Content Types

Successful JSON responses use:

```text
application/json
```

Image-analysis uploads use:

```text
multipart/form-data
```

Client-facing errors use:

```text
application/problem+json
```

## Health Endpoints

### Liveness

```http
GET /health/live
```

Purpose: confirm that the ASP.NET Core process is running and can answer requests.

The check shall not execute model inference or require a model artifact.

Planned success response:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "healthy"
}
```

### Readiness

```http
GET /health/ready
```

Purpose: indicate whether dependencies required for image analysis are available.

Before model integration exists, readiness may report only application readiness. After integration, it shall include a lightweight check of configured inference availability.

Planned ready response:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "ready"
}
```

A non-ready service returns `503 Service Unavailable` without exposing sensitive model paths or loader errors.

## Image Analysis

### Analyze One Image

```http
POST /api/v1/analyses
Content-Type: multipart/form-data
```

The initial endpoint accepts one image per request.

Multipart field:

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `image` | file | yes | Inspection image to analyze |

The initial supported media types and maximum upload size will be recorded after image validation is implemented. Clients must not assume that every format accepted by a browser is supported.

### Planned Success Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "model": {
    "id": "mvtec-ad-capsule-320",
    "category": "capsule"
  },
  "score": 4.992109,
  "threshold": 2.501822,
  "decision": "anomalous",
  "processingTimeMs": 1460,
  "traceId": "00-example-trace-id"
}
```

Field meanings:

| Field | Type | Description |
| --- | --- | --- |
| `model.id` | string | Stable identifier of the model or artifact used |
| `model.category` | string | Object category expected by the model |
| `score` | number | Image-level anomaly score produced by the configured aggregation |
| `threshold` | number | Decision threshold used for this result |
| `decision` | string | `normal` or `anomalous` |
| `processingTimeMs` | integer | Backend processing duration in milliseconds |
| `traceId` | string | Identifier for correlating logs and failures |

The decision is authoritative for clients. Clients shall not independently recalculate it from rounded response values.

## Localization Output

Localization is not part of the first required response. When implemented, it shall be added without changing the meaning of existing fields.

Possible representations include:

- a separate heatmap resource;
- encoded image data;
- a normalized numeric grid;
- a list of suspicious regions.

The representation remains open and shall not be added to the public contract until payload size, client needs, and model-runtime behavior are measured.

## Error Contract

Errors use ASP.NET Core-compatible Problem Details JSON.

Example:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
  "title": "Invalid image",
  "status": 400,
  "detail": "The uploaded file could not be decoded as a supported image.",
  "instance": "/api/v1/analyses",
  "traceId": "00-example-trace-id"
}
```

Public error details shall not contain stack traces, local file paths, process command lines, secrets, or raw model-loader messages.

## Planned Status Codes

| Status | Meaning |
| ---: | --- |
| `200 OK` | Request completed successfully |
| `400 Bad Request` | Missing, empty, malformed, or undecodable input |
| `413 Content Too Large` | Request exceeds the configured upload limit |
| `415 Unsupported Media Type` | Uploaded media type is not supported |
| `500 Internal Server Error` | Unexpected backend failure |
| `503 Service Unavailable` | Model or required inference dependency is unavailable |
| `504 Gateway Timeout` | Inference exceeded the configured execution time |

The final mapping between validation failures and `400` or `415` shall be verified during implementation.

## Compatibility Rules

- Existing field meanings shall not change within API version 1.
- New optional response fields may be added within version 1.
- Removing fields, changing field types, or changing decision semantics requires a new API version.
- Model replacement does not require a new API version when the response contract remains compatible and the model identity changes visibly.
- Clients shall not depend on property order.
- Clients shall not parse human-readable error text to determine behavior.

## Security and Privacy Rules

- Uploaded data is untrusted.
- File extension and declared content type alone are insufficient validation.
- Request size and execution time shall be bounded.
- Raw image data shall not be included in ordinary logs.
- Images shall not be persisted by default.
- Health and error responses shall not reveal trusted artifact locations or secrets.

## Example Values

Scores, thresholds, identifiers, durations, and trace identifiers shown in this document are illustrative unless explicitly tied to a verified integration test. They do not guarantee a particular result for arbitrary images.

## Open Contract Decisions

- supported image media types;
- maximum upload size;
- whether an optional model identifier may be selected by the caller;
- localization representation;
- whether processing time measures total request time or inference only;
- timeout error mapping for an in-process runtime versus an external service.

## Related Documentation

- `ProjectSpecification.md` – stable requirements
- `ArchitectureOverview.md` – component and dependency boundaries
- `DevelopmentStatus.md` – verified implementation status

## Last Updated

2026-08-14
