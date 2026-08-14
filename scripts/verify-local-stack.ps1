[CmdletBinding()]
param(
    [string]$BackendBaseUrl = "https://localhost:7056",

    [string]$PythonBaseUrl = "http://127.0.0.1:8000",

    [string]$ImagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-JsonRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Uri
    )

    $curlArguments = @(
        "--silent",
        "--show-error",
        "--fail-with-body"
    )

    if ($Uri.StartsWith("https://localhost", [StringComparison]::OrdinalIgnoreCase)) {
        $curlArguments += "--insecure"
    }

    $curlArguments += $Uri

    $response = & curl.exe @curlArguments

    if ($LASTEXITCODE -ne 0) {
        throw "Request failed: $Uri"
    }

    return $response | ConvertFrom-Json
}

Write-Host "Checking Python inference service..."
$pythonHealth = Invoke-JsonRequest -Uri "$PythonBaseUrl/health/live"

if ($pythonHealth.status -ne "healthy") {
    throw "Python inference service is not healthy."
}

Write-Host "Python inference service: healthy"

Write-Host "Checking backend liveness..."
$backendLiveness = Invoke-JsonRequest -Uri "$BackendBaseUrl/health/live"

if ($backendLiveness.status -ne "healthy") {
    throw "Backend is not healthy."
}

Write-Host "Backend liveness: healthy"

Write-Host "Checking backend readiness..."
$backendReadiness = Invoke-JsonRequest -Uri "$BackendBaseUrl/health/ready"

if ($backendReadiness.status -ne "ready") {
    throw "Backend is not ready."
}

Write-Host "Backend readiness: ready"

if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    Write-Host "No image supplied. Analysis check skipped."
    exit 0
}

$resolvedImagePath = (Resolve-Path -LiteralPath $ImagePath).Path
$imageExtension = [System.IO.Path]::GetExtension($resolvedImagePath)

$contentType = switch ($imageExtension.ToLowerInvariant()) {
    ".png" { "image/png" }
    ".jpg" { "image/jpeg" }
    ".jpeg" { "image/jpeg" }
    default {
        throw "Only PNG and JPEG images are supported."
    }
}

Write-Host "Running backend analysis..."

$analysisArguments = @(
    "--silent",
    "--show-error",
    "--fail-with-body",
    "--insecure",
    "--request",
    "POST",
    "$BackendBaseUrl/api/v1/analyses",
    "--form",
    "image=@$resolvedImagePath;type=$contentType"
)

$analysisResponse = & curl.exe @analysisArguments

if ($LASTEXITCODE -ne 0) {
    throw "Backend analysis request failed."
}

$analysis = $analysisResponse | ConvertFrom-Json

Write-Host "Analysis completed:"
Write-Host "  Model: $($analysis.model.id)"
Write-Host "  Category: $($analysis.model.category)"
Write-Host "  Score: $($analysis.score)"
Write-Host "  Threshold: $($analysis.threshold)"
Write-Host "  Decision: $($analysis.decision)"
Write-Host "  Processing time: $($analysis.processingTimeMs) ms"
Write-Host "  Trace ID: $($analysis.traceId)"