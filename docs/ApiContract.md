# Industrial Visual Anomaly Detection Backend – API Contract

## Purpose

This document defines the implemented public HTTP contract of the backend.

Only stable or deliberately selected contract elements belong here. Implementation progress belongs in `DevelopmentStatus.md`, component boundaries in `ArchitectureOverview.md`, and Python runtime integration in `ModelIntegrationStrategy.md`.

## Contract Status

**Implemented – API version 1 with model discovery and selection**

The health, model-catalog, and single-image analysis endpoints are implemented, covered by automated tests, represented in OpenAPI, and verified through local multi-model backend-to-Python workflows including PNG heatmap transport.

This is a portfolio and development contract, not a production service-level agreement.

## Base URLs

The checked-in development profiles use:

```text
HTTPS: https://localhost:7056
HTTP:  http://localhost:5070
```

Deployment hosts and ports may differ. Endpoint paths and payload contracts remain unchanged.

## General Conventions

- API routes use lowercase path segments.
- Application endpoints are versioned under `/api/v1`.
- Health endpoints are operational and unversioned.
- JSON property names use `camelCase`.
- Durations are explicit milliseconds.
- Unknown JSON response properties should be ignored for forward compatibility.
- HTTP status is the primary transport outcome.
- Clients use structured fields rather than parsing human-readable text.
- Scores and thresholds are JSON numbers.
- Successful analysis responses contain the backend trace identifier.
- Stable model identifiers, not display names or category names, select runtimes.

## Content Types

```text
Successful JSON:             application/json
Image-analysis request:     multipart/form-data
Client-facing errors:       application/problem+json
```

## Health Endpoints

### Liveness

```http
GET /health/live
```

Confirms that the ASP.NET Core process can answer requests. It does not contact Python or execute inference.

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

Reports whether the configured Python health endpoint is reachable and healthy. It does not retrieve the catalog or execute a prediction.

Ready:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "ready"
}
```

Not ready:

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json
```

```json
{
  "status": "not_ready"
}
```

Health responses do not expose Python URLs, registry or artifact locations, connection errors, or loader details.

## Model Catalog

### Get Available Models

```http
GET /api/v1/models
```

OpenAPI operation metadata:

| Property | Value |
| --- | --- |
| Operation ID | `GetInferenceModels` |
| Summary | `Get available inference models` |

The backend requests the internal Python catalog and maps it into a client-neutral public response. Python is authoritative for availability and default selection.

### Success Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

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
    },
    {
      "id": "visa-cashew-generalized-q95-320",
      "displayName": "VisA - Cashew",
      "category": "cashew",
      "inputSize": 320,
      "isDefault": false
    }
  ]
}
```

### Catalog Fields

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `defaultModelId` | string | yes | Non-empty ID of the default available model |
| `models` | array | yes | Non-empty ordered collection of available models |
| `models[].id` | string | yes | Stable non-empty model selection identifier |
| `models[].displayName` | string | yes | Human-readable presentation name |
| `models[].category` | string | yes | Artifact-defined product category |
| `models[].inputSize` | integer | yes | Positive square model input size |
| `models[].isDefault` | Boolean | yes | Whether the entry is the default |

The catalog must contain exactly one default entry whose `id` equals `defaultModelId`. Clients should preserve the returned order for presentation unless they have a deliberate user-interface rule.

### Catalog Status Codes

| Status | Condition |
| ---: | --- |
| `200 OK` | Catalog retrieved and validated successfully |
| `500 Internal Server Error` | Unexpected unhandled backend failure |
| `503 Service Unavailable` | Python is unavailable, times out, fails, or returns an invalid catalog |

### Catalog Unavailable

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json
```

```json
{
  "type": "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
  "title": "Inference unavailable",
  "status": 503,
  "detail": "The anomaly inference service is currently unavailable.",
  "instance": "/api/v1/models",
  "traceId": "0HNNQ2F8C9UQT:00000001"
}
```

The public category intentionally hides transport and internal catalog-validation details.

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

### Multipart Request

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `image` | binary file | yes | PNG or JPEG image to analyze |
| `modelId` | string | no | Stable model ID returned by `GET /api/v1/models` |

Example:

```powershell
curl.exe `
    --insecure `
    --request POST `
    https://localhost:7056/api/v1/analyses `
    --form "image=@C:\path\to\image.png;type=image/png" `
    --form "modelId=mvtec-ad-capsule-320"
```

Selection behavior:

- a non-empty `modelId` is forwarded unchanged to Python;
- an omitted, empty, or whitespace-only `modelId` is treated as not specified;
- when no ID is forwarded, Python selects its configured default;
- clients should use only IDs returned by the current catalog;
- display names and categories are not selection identifiers.

### Upload Constraints

| Constraint | Default |
| --- | ---: |
| Maximum image-file size | 10,485,760 bytes (10 MiB) |
| Maximum multipart request-body size | 11,534,336 bytes (11 MiB) |
| PNG media type | `image/png` |
| JPEG media type | `image/jpeg` |

The backend validates presence, non-empty content, file size, media type, signature, and agreement between media type and signature. Python performs complete decoding, so signature-valid but unreadable data may still be rejected.

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
  "score": 4.992109,
  "threshold": 2.501822,
  "decision": "anomalous",
  "processingTimeMs": 1199,
  "traceId": "0HNNVDI4958NA:00000001",
  "heatmap": {
    "contentType": "image/png",
    "width": 320,
    "height": 320,
    "dataBase64": "<base64-encoded PNG>"
  }
}
```

### Success Fields

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `model` | object | yes | Model that actually produced the result |
| `model.id` | string | yes | Stable non-empty model identifier |
| `model.category` | string | yes | Non-empty artifact category |
| `score` | number | yes | Finite non-negative anomaly score |
| `threshold` | number | yes | Finite non-negative decision threshold |
| `decision` | string | yes | `normal` or `anomalous` |
| `processingTimeMs` | integer | yes | Non-negative backend controller duration |
| `traceId` | string | yes | Backend diagnostic request identifier |
| `heatmap` | object | yes | Model-generated anomaly heatmap |
| `heatmap.contentType` | string | yes | Currently `image/png` |
| `heatmap.width` | integer | yes | Positive width |
| `heatmap.height` | integer | yes | Positive height |
| `heatmap.dataBase64` | string | yes | Non-empty valid Base64 PNG representation |

When `modelId` is supplied, clients should verify or record `model.id` from the response as the authoritative identity of the runtime used.

The decision rule is:

```text
score > threshold  => anomalous
score <= threshold => normal
```

The backend validates service consistency. Clients should treat `decision` as authoritative rather than recomputing it from reformatted values.

`processingTimeMs` starts immediately before analyzer invocation and stops when inference returns. It excludes model-service startup and is not complete client-observed round-trip time.

## Trace Identifier

The backend uses `HttpContext.TraceIdentifier` for analysis requests. It is returned in successful analyses and Problem Details, included in structured logs, and forwarded to Python as `X-Correlation-ID`.

The API does not adopt a client-supplied correlation header. Clients should report the returned `traceId` for diagnostics.

## Problem Details Contract

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

| Field | Type | Description |
| --- | --- | --- |
| `type` | string | Stable problem-category URI when mapped |
| `title` | string | Human-readable summary |
| `status` | integer | HTTP status code |
| `detail` | string | Human-readable explanation |
| `instance` | string | Request path |
| `traceId` | string | Backend trace identifier |

Clients should primarily use status and stable `type`, not exact human-readable wording. Public errors omit stack traces, local paths, Python exception details, registry contents, and artifact-loader messages.

## Analysis Status Codes

| Status | Condition |
| ---: | --- |
| `200 OK` | Analysis completed successfully |
| `400 Bad Request` | Missing, empty, signature-invalid, or unreadable image |
| `413 Payload Too Large` | File or request exceeds the configured limit |
| `415 Unsupported Media Type` | Declared image media type is unsupported |
| `500 Internal Server Error` | Unexpected unhandled backend failure |
| `503 Service Unavailable` | Python is unavailable, times out, rejects the model, fails, or returns an invalid response |

The current backend does not expose a dedicated public unknown-model status. A Python unknown-model response crosses the adapter boundary as inference unavailable and maps to `503 Service Unavailable`.

Adapter timeouts map to `503`, not `504`. Caller cancellation is propagated and is not intentionally converted into inference unavailable.

## Validation Failure Mapping

### Missing or Empty Image

Missing and empty files return `400 Bad Request` with an invalid-image Problem Details category.

### Invalid or Mismatched Signature

Returns `400 Bad Request` with detail indicating that content does not match the declared type.

### Unreadable Decoded Image

When Python returns `400 Bad Request` for image decoding, the backend maps it to public `400 Bad Request` with the stable invalid-image category.

### Image Too Large

Returns `413 Payload Too Large`. Server-level request limits may reject the request before controller Problem Details mapping.

### Unsupported Media Type

Returns `415 Unsupported Media Type` with the stable unsupported-image-type category.

### Inference Unavailable

Returns `503 Service Unavailable` for:

- unreachable Python service;
- adapter timeout;
- unsuccessful Python status other than mapped image `400`;
- unknown model identifier returned as an unsuccessful Python status;
- empty or malformed JSON;
- invalid model ID or category;
- negative or non-finite score or threshold;
- decision inconsistent with score and threshold;
- missing or invalid heatmap metadata or Base64 data.

## Localization Output

Every successful version-1 analysis returns one threshold-normalized Base64 RGB PNG heatmap at model-input dimensions.

It is intended for display and diagnostic interpretation and does not replace `decision`. The response does not contain raw patch scores, segmentation masks, regions, boxes, or a pre-blended overlay.

Future localization additions must preserve existing meanings or introduce a new API version.

## Model Selection Contract

- Clients retrieve available models through `GET /api/v1/models`.
- The catalog identifies the current default model.
- Clients may submit one catalog model ID with an analysis.
- Omitted or blank selection delegates to the Python default.
- The backend does not select by category or display name.
- The backend does not verify catalog membership before forwarding a non-empty ID.
- Python remains authoritative for runtime resolution.
- The analysis response identifies the actual model used.
- Adding or replacing models does not require a new API version while field meanings remain compatible.

## CORS Behavior

CORS uses an explicit allowlist. No browser origin is allowed by default. Configured HTTP or HTTPS origins may use required methods and headers. Origins with paths, queries, or fragments are rejected during startup.

CORS does not replace authentication or authorization.

## OpenAPI Contract

The Development OpenAPI document is available at:

```text
/openapi/v1.json
```

It represents the catalog operation and analysis multipart fields, including binary `image`, optional `modelId`, response schemas, heatmap payload, and declared statuses.

Availability outside Development is not part of the contract.

## Compatibility Rules

- Existing field meanings do not change within version 1.
- Additive response fields may be introduced when clients can ignore unknown properties.
- Removing fields, changing types, or changing decision semantics requires a new API version.
- Adding catalog entries or changing the default does not require a new API version.
- Clients do not depend on property or catalog order unless order is deliberately used for presentation.
- Clients do not parse human-readable errors to determine behavior.
- Clients tolerate additional Problem Details extensions.
- Operational configuration may change limits, dependency locations, and allowed origins.

## Security and Privacy Rules

- Uploads are untrusted.
- Extension and declared content type alone are insufficient validation.
- Request size, file size, and adapter time are bounded.
- Raw images are not logged or persisted by default.
- Health and errors do not reveal registry or artifact locations.
- The backend never accepts registry or artifact uploads.
- Authentication and authorization are not implemented.
- Production exposure requires network security, access control, and abuse protection.

## Deferred Contract Decisions

- dedicated public unknown-model error mapping;
- additional localization forms;
- batch analysis;
- authentication and authorization;
- persistence and history resources;
- client correlation-ID adoption;
- rate-limit behavior;
- caching or asynchronous jobs.

## Related Documentation

- `ProjectSpecification.md` – stable requirements
- `ArchitectureOverview.md` – component and dependency boundaries
- `ModelIntegrationStrategy.md` – Python integration
- `DevelopmentStatus.md` – verified implementation status

## Last Updated

2026-08-21
