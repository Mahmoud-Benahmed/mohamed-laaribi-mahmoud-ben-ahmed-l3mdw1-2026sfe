param (
    [string]$InfraProject = $null,
    [string]$StartupProject = $null,
    [string]$DbContext = $null,
    [string]$MigrationName = $null
)

# =========================
# RESOLVE PROJECT PATHS FIRST
# =========================
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-CsprojPath {
    param ([string]$inputPath)

    # 1. Try relative to script root (where reset-db.ps1 lives)
    $fullPath = Join-Path $scriptRoot $inputPath
    if (Test-Path $fullPath) {
        if ($fullPath.EndsWith(".csproj")) {
            return (Resolve-Path $fullPath).Path
        }
        $csproj = Get-ChildItem -Path $fullPath -Filter *.csproj -Recurse | Select-Object -First 1
        if ($csproj) {
            return $csproj.FullName
        }
    }

    # 2. Search entire solution (fallback)
    Write-Host "Searching for $inputPath in solution..." -ForegroundColor DarkYellow
    $found = Get-ChildItem -Path $scriptRoot -Recurse -Filter *.csproj |
        Where-Object { $_.Name -eq $inputPath } |
        Select-Object -First 1

    if ($found) {
        return $found.FullName
    }

    throw "Could not resolve project: $inputPath"
}

# Resolve both projects (they might be the same)
$infraPath = Resolve-CsprojPath $InfraProject
$startupPath = Resolve-CsprojPath $StartupProject

Write-Host "Infra Project: $infraPath" -ForegroundColor Cyan
Write-Host "Startup Project: $startupPath" -ForegroundColor Cyan

# =========================
# LOAD CONFIG FROM PROJECT FOLDER (if exists)
# =========================
$projectDir = Split-Path $infraPath -Parent
$appSettingsPath = Join-Path $projectDir "appsettings.json"

if (Test-Path $appSettingsPath) {
    Write-Host "Loading config from $appSettingsPath" -ForegroundColor DarkGray
    $appSettings = Get-Content $appSettingsPath | ConvertFrom-Json
    $efReset = $appSettings.EfReset

    if (-not $InfraProject -and $efReset.Project) { $InfraProject = $efReset.Project }
    if (-not $StartupProject -and $efReset.Project) { $StartupProject = $efReset.Project }
    if (-not $DbContext -and $efReset.DbContext) { $DbContext = $efReset.DbContext }
    if (-not $MigrationName -and $efReset.MigrationName) { $MigrationName = $efReset.MigrationName }
} else {
    Write-Host "No appsettings.json found in project folder. Using parameters only." -ForegroundColor DarkYellow
}

# =========================
# VALIDATE REQUIRED PARAMETERS
# =========================
if (-not $DbContext) { throw "DbContext is required (provide via -DbContext or appsettings.json:EfReset.DbContext)" }
if (-not $MigrationName) { throw "MigrationName is required (provide via -MigrationName or appsettings.json:EfReset.MigrationName)" }

# =========================
# DROP DB
# =========================
Write-Host "Dropping database..." -ForegroundColor Yellow
$dropArgs = @(
    "database", "drop", "--force",
    "--project", $infraPath,
    "--startup-project", $startupPath,
    "--context", $DbContext
)
& dotnet ef $dropArgs
if ($LASTEXITCODE -ne 0) { Write-Host "Drop failed, continuing..." -ForegroundColor DarkYellow }

# =========================
# DELETE MIGRATIONS
# =========================
$migrationsPath = Join-Path $projectDir "Migrations"

Write-Host "Deleting Migrations folder..." -ForegroundColor Yellow
if (Test-Path $migrationsPath) {
    Remove-Item -Recurse -Force $migrationsPath
    Write-Host "Migrations folder deleted." -ForegroundColor DarkYellow
} else {
    Write-Host "No Migrations folder found." -ForegroundColor DarkYellow
}

# =========================
# ADD MIGRATION
# =========================
Write-Host "Adding migration: $MigrationName..." -ForegroundColor Yellow
$addArgs = @(
    "migrations", "add", $MigrationName,
    "--project", $infraPath,
    "--startup-project", $startupPath,
    "--context", $DbContext
)
& dotnet ef $addArgs
if ($LASTEXITCODE -ne 0) { throw "Migration add failed" }

# =========================
# UPDATE DB
# =========================
Write-Host "Updating database..." -ForegroundColor Yellow
$updateArgs = @(
    "database", "update",
    "--project", $infraPath,
    "--startup-project", $startupPath,
    "--context", $DbContext
)
& dotnet ef $updateArgs
if ($LASTEXITCODE -ne 0) { throw "Database update failed" }

Write-Host "Done." -ForegroundColor Green