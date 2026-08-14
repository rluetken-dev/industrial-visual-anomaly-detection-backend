# Commit Message Guidelines

This project follows the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification.

## Format

```text
<type>(optional scope): <short summary>

(optional body)

(optional footer)
```

## Types

- `feat` – add a capability or observable behavior
- `fix` – correct a bug or invalid behavior
- `docs` – change documentation only
- `test` – add or update automated tests
- `refactor` – restructure code without changing intended behavior
- `perf` – improve measured performance without changing intended behavior
- `style` – change formatting or whitespace only
- `chore` – change tooling, dependencies, configuration, or repository support files
- `revert` – revert an earlier commit

## Recommended Scopes

- `api` – controllers, routes, HTTP models, and status-code behavior
- `health` – liveness and readiness behavior
- `validation` – request, upload, and image validation
- `inference` – application-level inference orchestration and abstractions
- `model` – model metadata and runtime contract handling
- `artifacts` – artifact loading, compatibility, and integrity checks
- `config` – application configuration and options
- `errors` – Problem Details and failure mapping
- `observability` – logging, tracing, metrics, and timing
- `openapi` – generated API description and documentation
- `security` – security controls and dependency remediation
- `ci` – automated workflows and repository checks
- `deps` – dependency updates
- `tests` – shared test infrastructure
- `readme` – repository README
- `docs` – documentation spanning multiple documents
- `architecture` – architecture documentation
- `contract` – API contract documentation
- `spec` – project specification
- `status` – development-status documentation

Scopes are optional. Use the most specific useful scope and keep each commit focused on one logical change.

## Examples

```text
feat(health): add liveness endpoint
```

```text
feat(validation): reject empty image uploads
```

```text
feat(inference): add anomaly analyzer abstraction
```

```text
feat(api): add versioned image analysis endpoint
```

```text
fix(errors): preserve trace identifier in problem details
```

```text
test(api): cover unsupported image media type
```

```text
docs(contract): define initial analysis response
```

```text
chore(ci): add dotnet build and test workflow
```

```text
chore(deps): update openapi package
```

## Guidelines

- Use lowercase for type and scope.
- Write the summary in imperative mood, such as `add`, not `added`.
- Do not end the summary with a period.
- Keep the summary concise, ideally no longer than 72 characters.
- Keep each commit focused on one logical change.
- Use the body for motivation, trade-offs, or migration details.
- Do not include secrets, credentials, private hosts, trusted artifact paths, or personal machine paths.
- Do not commit uploaded images, datasets, model artifacts, generated heatmaps, logs, or local runtime output.
- Do not describe planned endpoints or model integration as implemented before verification.
- Do not claim latency, throughput, parity, or security improvements without recorded evidence.
- Separate behavioral changes from bulk formatting where practical.

## Breaking Changes

Mark a breaking change when a public API, configuration schema, error contract, or model-integration contract requires consumers to migrate.

```text
feat(api)!: replace analysis response schema
```

Alternatively, use a footer:

```text
feat(config): rename inference options

BREAKING CHANGE: deployments must replace the previous configuration keys.
```

Internal changes before a public contract exists are not automatically breaking changes.

## Documentation Commits

Use `docs` when only documentation changes:

```text
docs(architecture): document inference boundary
```

Use `chore` when documentation is only one part of broader repository initialization:

```text
chore: initialize backend repository
```

## Test Commits

Use the affected capability as the scope when tests belong to one area:

```text
test(health): cover readiness failure
```

Use `tests` for broad fixtures or shared test infrastructure:

```text
test(tests): add web application factory fixture
```

## Dependency Commits

Use `chore(deps)` when a dependency update does not directly implement product behavior.

If a dependency change affects public behavior, model results, serialization, or compatibility, validate the change and explain it in the commit body.

## Initial Repository Commit

Use:

```text
chore: initialize backend repository
```

Create the initial commit after:

- the solution builds successfully;
- generated build output is ignored;
- secrets and machine-specific settings are excluded;
- initial documentation is present;
- the repository contains no datasets, uploaded images, or model artifacts.
