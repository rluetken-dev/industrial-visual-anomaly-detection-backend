# Industrial Visual Anomaly Detection Backend – API Contract

## Purpose

This document defines the implemented public HTTP contract of the backend.

Only stable or deliberately selected contract elements belong here. Implementation progress belongs in `DevelopmentStatus.md`, component boundaries in `ArchitectureOverview.md`, and Python runtime integration in `ModelIntegrationStrategy.md`.

## Contract Status

**Implemented – API version 1 baseline**

The health and single-image analysis endpoints documented here are implemented, covered by automated integration tests, represented in OpenAPI, and verified through a real local backend-to-Python inference flow.

This is a portfolio and development contract, not a production service-level agreement.

## Base URLs

The checked-in ASP.NET Core development profiles use:

```text
HTTPS: https://localhost:7056
HTTP:  http://localhost:5070
```

Deployment environments may use different hosts, ports, reverse proxies, and TLS termination. Endpoint paths and payload contracts remain unchanged.

## General Conventions

- API routes use lowercase path segments.
- Application endpoints are versioned under `/api/v1`.
- Health endpoints are operational endpoints and are not placed under the application API version.
- JSON property names use `camelCase`.
- Durations are represented explicitly in milliseconds.
- Unknown JSON response properties should be ignored by clients for forward compatibility.
- Clients use the HTTP status code as the primary transport outcome.
- Clients use structured response fields rather than parsing human-readable text.
- Numeric score and threshold values are JSON numbers.
- Successful analysis responses contain the backend trace identifier.

## Content Types

Successful JSON responses use:

```text
application/json
```

Image-analysis uploads use:

```text
multipart/form-data
```

Client-facing Problem Details responses use:

```text
application/problem+json
```

## Health Endpoints

### Liveness

```http
GET /health/live
```

Purpose: confirm that the ASP.NET Core process is running and can answer requests.

The check does not contact the Python service or execute model inference.

#### Success Response

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

Purpose: report whether the dependency required for image analysis is currently reachable and healthy.

The backend performs a lightweight request to the configured Python health endpoint. It does not execute a prediction.

#### Ready Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "ready"
}
```

#### Not-Ready Response

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json
```

```json
{
  "status": "not_ready"
}
```

The response does not expose the configured Python URL, artifact location, connection error, or model-loader details.

## Image Analysis

### Analyze One Image

```http
POST /api/v1/analyses
Content-Type: multipart/form-data
```

OpenAPI operation metadata:

| Property | Value |
| --- | --- |
| Operation ID | `AnalyzeImage` |
| Summary | `Analyze an industrial image` |

The endpoint accepts exactly one image field per analysis request.

### Multipart Request

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `image` | binary file | yes | PNG or JPEG inspection image to analyze |

Example using `curl.exe`:

```powershell
curl.exe `
    --insecure `
    -X POST `
    https://localhost:7056/api/v1/analyses `
    -F "image=@C:\path\to\image.png;type=image/png"
```

### Default Upload Constraints

| Constraint | Default |
| --- | ---: |
| Maximum image-file size | 10,485,760 bytes (10 MiB) |
| Maximum multipart request-body size | 11,534,336 bytes (11 MiB) |
| Supported PNG media type | `image/png` |
| Supported JPEG media type | `image/jpeg` |

The limits are configurable. A deployment may lower them without changing the response schema.

The backend validates:

- field presence;
- non-empty content;
- file size;
- declared media type;
- PNG or JPEG file signature;
- agreement between media type and signature.

The Python service performs actual image decoding. A file can therefore pass signature validation and still be rejected as unreadable during inference preparation.

### Success Response

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
  "score": 4.992109298706055,
  "threshold": 2.501821517944336,
  "decision": "anomalous",
  "processingTimeMs": 1692,
  "traceId": "0HNNQ2F8C9UQT:00000001"
}
```

### Success Fields

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `model` | object | yes | Identity of the model used for the analysis |
| `model.id` | string | yes | Non-empty model or artifact identifier |
| `model.category` | string | yes | Non-empty object category expected by the model |
| `score` | number | yes | Finite, non-negative image-level anomaly score |
| `threshold` | number | yes | Finite, non-negative decision threshold |
| `decision` | string | yes | `normal` or `anomalous` |
| `processingTimeMs` | integer | yes | Non-negative backend analysis duration in milliseconds |
| `traceId` | string | yes | Backend request identifier for diagnostic correlation |

The model decision rule in the current runtime is:

```text
score > threshold  => anomalous
score <= threshold => normal
```

The backend validates that the service decision is consistent with this rule. Clients should still treat `decision` as authoritative and must not recalculate it from rounded or reformatted values.

`processingTimeMs` measures processing inside the controller from immediately before invoking the analyzer until the inference result is returned. It is not a complete client-observed round-trip duration and does not include model-service startup.

## Trace Identifier

The backend uses `HttpContext.TraceIdentifier` as the request identity for analysis.

The identifier is:

- included in the successful analysis response as `traceId`;
- included in Problem Details extensions;
- included in structured backend logs;
- forwarded to the Python service as the `X-Correlation-ID` request header.

The current API does not define adoption of a client-supplied `X-Correlation-ID` as the backend trace identifier. Clients must therefore read the returned `traceId` when reporting a request for diagnostics.

## Error Contract

Analysis errors use ASP.NET Core-compatible Problem Details JSON where the request reaches the application error boundary.

Typical shape:

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
  "title": "Invalid image",
  "status": 400,
  "detail": "The uploaded file is not a readable image.",
  "instance": "/api/v1/analyses",
  "traceId": "0HNNQ2F8C9UQT:00000001"
}
```

### Problem Details Fields

| Field | Type | Description |
| --- | --- | --- |
| `type` | string | Stable problem-category URI when explicitly mapped |
| `title` | string | Short human-readable summary |
| `status` | integer | HTTP status code |
| `detail` | string | Human-readable explanation |
| `instance` | string | Request path associated with the failure |
| `traceId` | string | Backend trace identifier |

Human-readable `title` and `detail` text may be refined without creating a new API version. Programmatic clients should primarily use HTTP status, the stable `type` URI when present, and structured fields.

Public errors do not contain stack traces, local file paths, process command lines, secrets, Python exception details, or artifact-loader messages.

## Analysis Status Codes

| Status | Condition |
| ---: | --- |
| `200 OK` | Analysis completed successfully |
| `400 Bad Request` | Missing, empty, signature-invalid, or unreadable image |
| `413 Payload Too Large` | Image or multipart request exceeds the configured limit |
| `415 Unsupported Media Type` | Declared image media type is not supported |
| `500 Internal Server Error` | Unexpected unhandled backend failure |
| `503 Service Unavailable` | Python inference is unavailable, times out, fails, or returns an invalid response |

Adapter timeouts currently map to `503 Service Unavailable`, not `504 Gateway Timeout`.

Cancellation caused by the caller is propagated through the asynchronous pipeline and is not intentionally converted into an inference-unavailable Problem Details response.

## Validation Failure Mapping

### Missing Image Field

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
```

ASP.NET Core model binding produces a validation Problem Details response. Its title is:

```text
One or more validation errors occurred.
```

Clients should not depend on the exact generated validation-error dictionary beyond recognizing the `400` outcome.

### Empty Image

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
  "title": "Invalid image",
  "status": 400,
  "detail": "The uploaded image file must not be empty.",
  "instance": "/api/v1/analyses"
}
```

### Invalid or Mismatched File Signature

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
  "title": "Invalid image",
  "status": 400,
  "detail": "The uploaded file content does not match its declared image type.",
  "instance": "/api/v1/analyses"
}
```

### Unreadable Decoded Image

This response is used when the upload passes the bounded backend checks but the Python service cannot decode it as an image.

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
  "title": "Invalid image",
  "status": 400,
  "detail": "The uploaded file is not a readable image.",
  "instance": "/api/v1/analyses"
}
```

### Image Too Large

```http
HTTP/1.1 413 Payload Too Large
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/image-too-large",
  "title": "Image too large",
  "status": 413,
  "detail": "The uploaded image exceeds the configured size limit.",
  "instance": "/api/v1/analyses"
}
```

If the entire HTTP request exceeds the server-level limit before controller execution, Kestrel or multipart handling may generate the rejection earlier than the controller-level Problem Details mapping. Clients must rely primarily on status `413` for this case.

### Unsupported Media Type

```http
HTTP/1.1 415 Unsupported Media Type
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/unsupported-image-type",
  "title": "Unsupported image type",
  "status": 415,
  "detail": "The uploaded file does not use a supported image content type.",
  "instance": "/api/v1/analyses"
}
```

### Inference Unavailable

```http
HTTP/1.1 503 Service Unavailable
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
  "title": "Inference unavailable",
  "status": 503,
  "detail": "The anomaly inference service is currently unavailable.",
  "instance": "/api/v1/analyses"
}
```

This public category intentionally combines several internal causes:

- Python service cannot be reached;
- configured adapter timeout expires;
- Python returns an unsuccessful status other than the mapped unreadable-image response;
- response JSON is empty or malformed;
- model ID or category is missing;
- score or threshold is negative or non-finite;
- decision is inconsistent with score and threshold.

## Localization Output

Localization is not part of API version 1. The current success response does not include patch scores, heatmaps, masks, regions, or image overlays.

If localization is added later, its representation must be selected based on measured payload size and actual client requirements. Existing version 1 field meanings must remain unchanged, or a new API version must be introduced.

## Model Selection

The caller cannot select a model or category in API version 1. The active model is determined by the configured Python service and its loaded artifact.

The response exposes `model.id` and `model.category` so clients can display and record which model produced the result.

Changing the deployed model does not by itself require a new API version when the response schema and field semantics remain compatible.

## CORS Behavior

CORS uses an explicit configuration allowlist.

- no browser origin is allowed by default;
- configured HTTP or HTTPS origins may use the methods and headers needed by the browser client;
- origins containing a path, query, or fragment are rejected during application startup;
- CORS behavior affects browsers and does not replace authentication or authorization.

CORS headers are deployment configuration, not part of the JSON response contract.

## OpenAPI Contract

The OpenAPI document is mapped in the Development environment at:

```text
/openapi/v1.json
```

Using the checked-in HTTPS profile:

```text
https://localhost:7056/openapi/v1.json
```

The document represents `image` as a binary multipart field and documents the analysis operation metadata and declared response status codes.

OpenAPI availability outside Development is not part of the current contract.

## Compatibility Rules

- Existing field meanings do not change within API version 1.
- New optional response fields may be added within version 1.
- Removing fields, changing field types, or changing decision semantics requires a new API version.
- Model replacement does not require a new API version when the HTTP contract remains compatible and the model identity changes visibly.
- Clients do not depend on JSON property order.
- Clients do not parse human-readable error text to determine behavior.
- Clients tolerate additional Problem Details extension fields.
- Operational configuration may change limits, dependency locations, and allowed origins without changing the API version.

## Security and Privacy Rules

- Uploaded data is untrusted.
- File extension and declared content type alone are insufficient validation.
- Request size, file size, and inference-adapter time are bounded.
- Raw image data is not included in ordinary logs.
- Images are not persisted by default.
- Health and error responses do not reveal trusted artifact locations, private service details, or secrets.
- The current API does not implement authentication or authorization.
- Production exposure requires deployment-specific network security, access control, and abuse protection.

## Example Values

Scores, thresholds, identifiers, durations, and trace identifiers shown in this document illustrate the verified contract shape. They do not guarantee a particular result for arbitrary images or future compatible model artifacts.

## Deferred Contract Decisions

- localization or heatmap representation;
- optional model selection;
- batch-analysis requests;
- authentication and authorization contract;
- persistence and analysis-history resources;
- client-submitted correlation-ID adoption;
- rate-limit response behavior;
- production caching or asynchronous job contracts.

These decisions do not block the initial web client.

## Related Documentation

- `ProjectSpecification.md` – stable requirements
- `ArchitectureOverview.md` – component and dependency boundaries
- `ModelIntegrationStrategy.md` – Python service integration
- `DevelopmentStatus.md` – verified implementation status

## Last Updated

2026-08-15
