# Industrial Visual Anomaly Detection Backend – Model Integration Strategy

## Purpose

This document defines how the backend will evaluate and select its model-inference boundary.

It does not select an implementation in advance. Verified implementation status belongs in `DevelopmentStatus.md`, while public HTTP behavior belongs in `ApiContract.md`.

## Starting Point

The separate Python model repository already provides:

- deterministic preprocessing;
- frozen ResNet18 feature extraction;
- patch-embedding construction;
- feature-memory scoring;
- image-level aggregation and thresholding;
- anomaly heatmaps;
- a versioned Python/PyTorch artifact;
- a single-image reference inference API and CLI;
- verified normal and anomalous Capsule reference predictions.

The current reference artifact contains `metadata.json` and `feature_memory.pt`. It is Python/PyTorch-specific and is not yet a framework-neutral production artifact.

Model repository:

<https://github.com/rluetken-dev/industrial-visual-anomaly-detection-model>

## Integration Goal

The backend requires an inference implementation that can:

1. accept validated image content;
2. apply behavior equivalent to the selected model configuration;
3. calculate patch and image anomaly scores;
4. apply the artifact threshold correctly;
5. return model identity, score, threshold, decision, and optional localization data;
6. support cancellation, bounded execution, diagnostics, and failure translation.

Controllers and public API contracts must remain independent of the chosen runtime.

## Stable Backend Boundary

The application will depend on an inference abstraction with conceptual input and output such as:

```text
Input
- image content
- optional media metadata
- cancellation request

Output
- model identifier
- category
- anomaly score
- threshold
- decision
- optional patch or localization result
- model execution duration
```

The exact C# interface will be introduced with the first analysis use case. Runtime-specific types shall not cross this boundary.

## Candidate A – Direct .NET Inference

The backend loads compatible model data and performs inference inside the ASP.NET Core process, potentially with ONNX Runtime for feature extraction and .NET code for supporting operations.

### Potential Advantages

- one deployable backend runtime;
- no separate Python service or process;
- direct cancellation, configuration, and health integration;
- simpler client-to-backend topology;
- potentially lower inter-process communication overhead.

### Risks and Required Work

- the selected 320 × 320 feature extractor still requires updated ONNX export and parity verification;
- preprocessing must be reproduced exactly in .NET;
- the current `feature_memory.pt` format is not directly framework-neutral;
- nearest-neighbor scoring and aggregation must match Python numerically;
- large feature memory affects process memory and startup behavior;
- artifact schema and compatibility validation must be expanded.

### Acceptance Evidence

- representative images produce scores within defined tolerances;
- normal/anomalous decisions match the Python reference;
- preprocessing parity is verified;
- memory usage and latency are measured;
- incompatible artifacts fail readiness safely.

## Candidate B – Python Inference Service

A separately running Python service owns the existing model runtime. The ASP.NET Core backend calls it through a local or network service contract.

### Potential Advantages

- reuses verified Python inference behavior;
- avoids immediate reimplementation of preprocessing and scoring;
- model-development and serving code can share Python components;
- easier adoption of future Python ML libraries.

### Risks and Required Work

- two deployed services and runtimes;
- service startup, health, timeout, and version coordination;
- network or local-service failure modes;
- separate security and configuration boundary;
- possible duplication between public backend and internal inference contracts;
- raw image transfer between processes or services.

### Acceptance Evidence

- internal service contract is versioned;
- timeouts and cancellations behave predictably;
- health and readiness distinguish backend and model-service failures;
- image data is not retained unexpectedly;
- end-to-end output matches direct Python reference inference.

## Candidate C – Controlled Python Process

The backend starts a Python command-line process for inference and exchanges bounded input and output.

### Intended Role

This option may be useful as a temporary local integration step. It is not automatically the preferred production architecture.

### Potential Advantages

- fastest reuse of the existing CLI;
- no internal network service required;
- useful for validating the backend abstraction before portable artifacts are ready.

### Risks and Required Work

- process startup overhead;
- command, path, timeout, and cleanup complexity;
- difficult concurrency and resource control;
- strict handling required to prevent command injection;
- machine-specific Python environment dependency;
- less suitable for scalable request processing.

### Acceptance Evidence

- executable and arguments are configured safely without shell interpolation;
- input and output use a strict machine-readable contract;
- timeouts terminate child processes reliably;
- concurrent request behavior is bounded;
- failures do not expose command lines or local paths.

## Selection Criteria

The candidates will be compared using:

- numerical and decision parity with Python reference inference;
- implementation complexity;
- deployment complexity;
- startup time;
- per-image latency;
- memory usage;
- cancellation and timeout behavior;
- concurrency control;
- health and readiness behavior;
- artifact portability and versioning;
- security and operational failure modes;
- maintainability for future model changes.

No candidate shall be selected only because it is fastest to prototype.

## Planned Evaluation Sequence

1. define the backend inference abstraction;
2. preserve fixed normal and anomalous Capsule reference cases;
3. establish the internal result contract;
4. implement the smallest controlled adapter spike;
5. measure parity, latency, memory, and failure behavior;
6. document the result and rejected alternatives;
7. select the first supported integration;
8. update architecture, configuration, readiness, and deployment documentation.

## Reference Cases

The initial parity checks shall include at least:

- a known normal Capsule image;
- a known anomalous Capsule `poke` image;
- the selected Capsule 320 × 320 artifact configuration;
- score, threshold, decision, and 40 × 40 patch-grid shape.

Reference images and dataset-derived artifacts remain local and are not committed to the backend repository.

## Artifact Considerations

The backend must not assume that every artifact version is compatible.

Compatibility checks may include:

- schema version;
- model and category identifier;
- input size;
- patch-grid size;
- embedding dimension;
- aggregation method and parameters;
- threshold;
- feature-memory representation and entry count;
- preprocessing contract;
- integrity information.

The current Python artifact does not yet provide a complete cross-runtime preprocessing contract. This gap must be resolved before direct .NET inference is treated as production-capable.

## Operational Rules

- trusted model locations come from configuration;
- model artifacts are not uploaded through the public analysis endpoint;
- raw loader exceptions are not returned to clients;
- readiness reflects required inference availability;
- model identity is included in analysis results and diagnostics;
- expensive initialization should not occur independently for every request;
- resource usage and concurrency shall be bounded.

## Decision Record

No integration candidate is selected yet.

When a candidate is selected, record:

- selected option and scope;
- measured evidence;
- accepted trade-offs;
- rejected alternatives;
- artifact and configuration requirements;
- rollback or replacement strategy;
- decision date and related commit.

## Related Documentation

- `ArchitectureOverview.md` – application and adapter boundaries
- `ApiContract.md` – public HTTP contract
- `ProjectSpecification.md` – stable backend requirements
- `DevelopmentStatus.md` – verified implementation progress

## Last Updated

2026-08-14
