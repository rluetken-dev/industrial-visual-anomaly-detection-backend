# Industrial Visual Anomaly Detection Backend

ASP.NET Core backend for industrial visual anomaly detection, image validation, model-inference orchestration, and client-neutral API integration.

The backend is intended to provide one stable HTTP boundary for future web and desktop clients. Model training and evaluation remain in the separate Python model repository.

> **Current status:** Early backend foundation. The .NET 10 solution and controller-based Web API project build successfully. Custom health, image-analysis, and model-integration functionality is not implemented yet.

## Goals

- expose a versioned image-analysis API;
- validate image uploads and enforce bounded request handling;
- isolate model-runtime details behind an inference abstraction;
- return anomaly score, threshold, decision, and model identity;
- support future web and desktop clients through one contract;
- provide automated tests, CI, and concise technical documentation.

## Planned Request Flow

```text
Web/Desktop client
        │
        ▼
ASP.NET Core API
        │
        ▼
Application orchestration
        │
        ▼
Inference abstraction
        │
        ▼
Selected model adapter
```

The concrete model adapter has not been selected. Direct .NET inference and Python-based integration remain under evaluation.

## Current Structure

```text
industrial-visual-anomaly-detection-backend/
├── src/
│   └── IndustrialVisualAnomalyDetection.Api/
├── docs/
│   ├── ApiContract.md
│   ├── ArchitectureOverview.md
│   ├── DevelopmentStatus.md
│   └── ProjectSpecification.md
├── IndustrialVisualAnomalyDetection.slnx
├── COMMITS.md
└── README.md
```

Test projects will be added under a top-level `tests` directory once the first custom backend behavior is implemented.

## Technology

- .NET 10
- ASP.NET Core Web API
- controller-based HTTP endpoints
- built-in dependency injection and configuration
- OpenAPI support from the project template

Planned additions will be selected only when required by implemented behavior.

## Prerequisites

- .NET SDK 10
- Git
- a supported model-inference dependency once integration is implemented

Verify the SDK:

```powershell
dotnet --version
```

## Build

From the repository root:

```powershell
dotnet restore .\IndustrialVisualAnomalyDetection.slnx
dotnet build .\IndustrialVisualAnomalyDetection.slnx
```

## Run the API

```powershell
dotnet run `
    --project .\src\IndustrialVisualAnomalyDetection.Api\IndustrialVisualAnomalyDetection.Api.csproj
```

The generated template endpoint is temporary and will be replaced by project-specific health and analysis endpoints.

Use the URLs printed by ASP.NET Core at startup. Local ports may vary by development configuration.

## Test

Once test projects exist:

```powershell
dotnet test .\IndustrialVisualAnomalyDetection.slnx
```

Large benchmark datasets and generated model artifacts will not be required for ordinary CI tests.

## Planned API

The draft contract currently defines:

```text
GET  /health/live
GET  /health/ready
POST /api/v1/analyses
```

No custom endpoint should be treated as implemented until it is recorded as verified in `docs/DevelopmentStatus.md`.

## Model Repository

Model development is maintained separately:

<https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

That repository owns:

- dataset qualification and deterministic splits;
- preprocessing and feature extraction;
- anomaly scoring and evaluation;
- threshold selection;
- heatmap generation;
- artifact export and Python reference inference.

This backend will consume a verified runtime contract without duplicating model-development logic.

## Documentation

- [Project Specification](docs/ProjectSpecification.md)
- [Architecture Overview](docs/ArchitectureOverview.md)
- [API Contract](docs/ApiContract.md)
- [Development Status](docs/DevelopmentStatus.md)
- [Commit Message Guidelines](COMMITS.md)

## Roadmap

1. run and inspect the generated API template;
2. establish repository hygiene and the initial Git history;
3. replace the example endpoint with liveness and readiness checks;
4. add the first API integration tests and CI workflow;
5. implement upload validation and a versioned analysis endpoint;
6. introduce the inference abstraction;
7. select and verify a concrete model adapter;
8. integrate future web and desktop clients.

## Security and Privacy

Uploaded images will be treated as untrusted input. File size, media type, decoded content, and processing time will be bounded before production-style use.

Raw images will not be persisted or logged by default. Secrets, trusted artifact locations, and machine-specific configuration must remain outside source control.

## Responsible Use

This project is an experimental and educational portfolio system. It is not a certified industrial inspection system and must not autonomously make production acceptance, safety, medical, or regulatory decisions.

## Repository License

No source-code license has been selected yet. Until a license is added, default copyright restrictions apply. Model artifacts, pretrained weights, datasets, and other third-party content remain subject to their own terms.
