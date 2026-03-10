#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes mjm.local.docs to a single folder ready for deployment.

.DESCRIPTION
    Builds and publishes the MCP server application with all dependencies.
    Supports both framework-dependent and self-contained deployments.

.PARAMETER OutputPath
    The output directory for published files (default: ./publish)

.PARAMETER SelfContained
    If specified, creates a self-contained deployment including .NET runtime

.PARAMETER SingleFile
    If specified, bundles everything into a single executable (requires -SelfContained)

.PARAMETER Runtime
    Target runtime identifier (default: win-x64)
    Common values: win-x64, win-x86, linux-x64, osx-x64, osx-arm64

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER CopyModel
    If specified, copies llama-server.exe and model files to publish folder and updates appsettings.json paths

.EXAMPLE
    .\publish.ps1
    Creates a framework-dependent deployment

.EXAMPLE
    .\publish.ps1 -SelfContained
    Creates a self-contained deployment with .NET runtime included

.EXAMPLE
    .\publish.ps1 -SelfContained -SingleFile -CopyModel
    Creates a single-file executable with model file copied

.EXAMPLE
    .\publish.ps1 -OutputPath C:\Deploy\LocalDocs -Runtime linux-x64 -SelfContained
    Creates a self-contained Linux deployment in custom folder
#>

param(
    [string]$OutputPath = "publish",
    [switch]$SelfContained,
    [switch]$SingleFile,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$CopyModel
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err { param([string]$Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

Write-Host "`nmjm.local.docs - Build & Publish Script`n" -ForegroundColor Magenta

# Validate we're in the right directory
if (-not (Test-Path "mjm.local.docs.sln")) {
    Write-Err "mjm.local.docs.sln not found. Please run this script from the mjm.local.docs directory."
    exit 1
}

# Validate single file requires self-contained
if ($SingleFile -and -not $SelfContained) {
    Write-Err "-SingleFile requires -SelfContained to be specified."
    exit 1
}

# Clean previous publish folder
if (Test-Path $OutputPath) {
    Write-Step "Cleaning existing publish folder..."
    Remove-Item -Recurse -Force $OutputPath
    Write-Success "Cleaned $OutputPath"
}

# Restore dependencies
Write-Step "Restoring NuGet packages..."
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Err "Failed to restore packages"
    exit $LASTEXITCODE
}
Write-Success "Packages restored"

# Build solution
Write-Step "Building solution..."
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Err "Build failed"
    exit $LASTEXITCODE
}
Write-Success "Build succeeded"

# Prepare publish arguments
$publishArgs = @(
    "publish",
    "src/Mjm.LocalDocs.Server/Mjm.LocalDocs.Server.csproj",
    "-c", $Configuration,
    "-o", $OutputPath,
    "--no-build"
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-r", $Runtime
    Write-Step "Publishing self-contained deployment for $Runtime..."
} else {
    $publishArgs += "--self-contained", "false"
    Write-Step "Publishing framework-dependent deployment..."
}

if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
    Write-Step "Creating single-file executable..."
}

# Publish
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Err "Publish failed"
    exit $LASTEXITCODE
}
Write-Success "Published to $OutputPath"

# Copy LlamaCpp dependencies if requested
if ($CopyModel) {
    Write-Step "Copying LlamaCpp executable and model files..."
    
    # Copy llama-server.exe
    $llamaServerSource = "src/llamacpp/llama-server.exe"
    if (Test-Path $llamaServerSource) {
        Copy-Item $llamaServerSource -Destination (Join-Path $OutputPath "llama-server.exe")
        Write-Success "llama-server.exe copied to publish folder"
    } else {
        Write-Warn "llama-server.exe not found at $llamaServerSource"
    }
    
    # Copy model file
    $modelSource = "src/llamacpp/v5-nano-retrieval-Q8_0.gguf"
    if (Test-Path $modelSource) {
        $modelDest = Join-Path $OutputPath "models"
        New-Item -ItemType Directory -Force -Path $modelDest | Out-Null
        Copy-Item $modelSource -Destination (Join-Path $modelDest "v5-nano-retrieval-Q8_0.gguf")
        Write-Success "Model copied to $modelDest"
    } else {
        Write-Warn "Model file not found at $modelSource"
    }
    
    # Update appsettings.json with correct paths
    Write-Step "Updating appsettings.json with production paths..."
    $appsettingsPath = Join-Path $OutputPath "appsettings.json"
    if (Test-Path $appsettingsPath) {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        if ($appsettings.LocalDocs.Embeddings.LlamaCpp) {
            $appsettings.LocalDocs.Embeddings.LlamaCpp.ExecutablePath = "./llama-server.exe"
            $appsettings.LocalDocs.Embeddings.LlamaCpp.ModelPath = "./models/v5-nano-retrieval-Q8_0.gguf"
            $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
            Write-Success "Updated appsettings.json with production paths"
        }
    }
}

# Create a run script in the publish folder
$runScriptPath = Join-Path $OutputPath "run.ps1"
$runScriptContent = @"
#!/usr/bin/env pwsh
# Auto-generated run script for mjm.local.docs

Write-Host "Starting mjm.local.docs MCP Server..." -ForegroundColor Cyan

if ($SelfContained -and $SingleFile) {
    $exeName = if ('$Runtime'.StartsWith('win')) { 'Mjm.LocalDocs.Server.exe' } else { './Mjm.LocalDocs.Server' }
    Write-Host "Running: `$exeName" -ForegroundColor Gray
    & `$exeName
} elseif ($SelfContained) {
    $exeName = if ('$Runtime'.StartsWith('win')) { 'Mjm.LocalDocs.Server.exe' } else { './Mjm.LocalDocs.Server' }
    Write-Host "Running: `$exeName" -ForegroundColor Gray
    & `$exeName
} else {
    Write-Host "Running: dotnet Mjm.LocalDocs.Server.dll" -ForegroundColor Gray
    dotnet Mjm.LocalDocs.Server.dll
}
"@
Set-Content -Path $runScriptPath -Value $runScriptContent -Encoding UTF8
Write-Success "Created run script: $runScriptPath"

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Gray
Write-Host "Publish Summary" -ForegroundColor Magenta
Write-Host ("=" * 60) -ForegroundColor Gray
Write-Host "Output Path:       $OutputPath"
Write-Host "Configuration:     $Configuration"
Write-Host "Self-Contained:    $SelfContained"
if ($SelfContained) {
    Write-Host "Runtime:           $Runtime"
}
Write-Host "Single File:       $SingleFile"
Write-Host "LlamaCpp Copied:   $CopyModel"
Write-Host ("=" * 60) -ForegroundColor Gray

# Instructions
Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. cd $OutputPath"
if ($SelfContained -and $SingleFile) {
    $exeName = if ($Runtime.StartsWith('win')) { "Mjm.LocalDocs.Server.exe" } else { "./Mjm.LocalDocs.Server" }
    Write-Host "  2. Run the application: $exeName"
} elseif ($SelfContained) {
    $exeName = if ($Runtime.StartsWith('win')) { "Mjm.LocalDocs.Server.exe" } else { "./Mjm.LocalDocs.Server" }
    Write-Host "  2. Run the application: $exeName"
} else {
    Write-Host "  2. Run the application: dotnet Mjm.LocalDocs.Server.dll"
}
Write-Host "  3. Access MCP endpoint: http://localhost:5000/mcp"
Write-Host "  4. Access Web UI: http://localhost:5000"
Write-Host ""

Write-Success "Publish completed successfully!"
Write-Host ""
