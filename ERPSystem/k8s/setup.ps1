#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

Write-Host "Running ERP environment setup..." -ForegroundColor Cyan

# ------------------------------------------------------------
# CONFIG
# ------------------------------------------------------------

$namespace = "erp"
$projectRoot = "C:\Users\SUDHT\source\repos\Projet\ERPSystem"
$hostsFile = "C:\Windows\System32\drivers\etc\hosts"

$services = @(
    @{Folder="ERP.TenantService";      Tag="tenant-service:latest"},
    @{Folder="ERP.AuthService";        Tag="auth-service:latest"},
    @{Folder="ERP.ArticleService";     Tag="article-service:latest"},
    @{Folder="ERP.ClientService";      Tag="client-service:latest"},
    @{Folder="ERP.FournisseurService"; Tag="fournisseur-service:latest"},
    @{Folder="ERP.StockService";       Tag="stock-service:latest"},
    @{Folder="ERP.InvoiceService";     Tag="invoice-service:latest"},
    @{Folder="ERP.PaymentService";     Tag="payment-service:latest"},
    @{Folder="ERP.Gateway";            Tag="gateway:latest"}
)

$hostnames = @(
    "erp.local",
    "acme.erp.local",
    "xyz.erp.local"
)

$deployments = @(
    "tenant-service",
    "auth-service",
    "article-service",
    "client-service",
    "fournisseur-service",
    "stock-service",
    "invoice-service",
    "payment-service",
    "gateway"
)

# ------------------------------------------------------------
# VALIDATION
# ------------------------------------------------------------

Write-Host "Validating dependencies..." -ForegroundColor Cyan

$requiredCommands = @("kubectl", "minikube", "docker")

foreach ($cmd in $requiredCommands) {

    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "$cmd is not installed or not available in PATH."
    }
}

# ------------------------------------------------------------
# ENSURE MINIKUBE IS RUNNING
# ------------------------------------------------------------

Write-Host "Checking Minikube status..." -ForegroundColor Cyan

$minikubeStatus = minikube status --format="{{.Host}}" 2>$null

if ($minikubeStatus -ne "Running") {

    Write-Host "Minikube is not running. Starting Minikube..." -ForegroundColor Yellow

    minikube start

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start Minikube."
    }
}

# ------------------------------------------------------------
# ENABLE INGRESS
# ------------------------------------------------------------

Write-Host "Ensuring Minikube ingress addon is enabled..." -ForegroundColor Cyan

minikube addons enable ingress | Out-Null

# ------------------------------------------------------------
# DELETE NAMESPACE
# ------------------------------------------------------------

Write-Host "Deleting namespace '$namespace'..." -ForegroundColor Yellow

kubectl delete namespace $namespace --ignore-not-found=true --wait=true

Write-Host "Waiting for namespace deletion..." -ForegroundColor Yellow

$maxAttempts = 60
$attempt = 0

do {

    Start-Sleep -Seconds 2

    $ns = kubectl get namespace $namespace --ignore-not-found

    $attempt++

    if ($attempt -ge $maxAttempts) {
        throw "Namespace deletion timed out."
    }

} while ($ns)

Write-Host "Namespace deleted." -ForegroundColor Green

# ------------------------------------------------------------
# CREATE NAMESPACE
# ------------------------------------------------------------

Write-Host "Creating namespace..." -ForegroundColor Cyan

kubectl apply -f k8s/namespace.yaml

# ------------------------------------------------------------
# GENERATE SECRETS
# ------------------------------------------------------------

Write-Host "Generating secrets..." -ForegroundColor Cyan

& ".\k8s\secrets\create-secrets.ps1"

if ($LASTEXITCODE -ne 0) {
    throw "Secrets generation failed."
}

# ------------------------------------------------------------
# DEPLOY INFRASTRUCTURE
# ------------------------------------------------------------

Write-Host "Deploying infrastructure..." -ForegroundColor Cyan

kubectl apply -f k8s/infra/

# ------------------------------------------------------------
# WAIT FOR INFRASTRUCTURE
# ------------------------------------------------------------

Write-Host "Waiting for SQL Server..." -ForegroundColor Yellow

kubectl wait `
    --namespace=$namespace `
    --for=condition=ready pod `
    --selector=app=sqlserver `
    --timeout=180s

Write-Host "Waiting for MongoDB..." -ForegroundColor Yellow

kubectl wait `
    --namespace=$namespace `
    --for=condition=ready pod `
    --selector=app=mongo `
    --timeout=180s

Write-Host "Waiting for Kafka..." -ForegroundColor Yellow

kubectl wait `
    --namespace=$namespace `
    --for=condition=ready pod `
    --selector=app=kafka `
    --timeout=180s

Write-Host "Waiting additional time for Kafka stabilization..." -ForegroundColor Yellow

Start-Sleep -Seconds 20

# ------------------------------------------------------------
# USE MINIKUBE DOCKER DAEMON
# ------------------------------------------------------------

Write-Host "Switching to Minikube Docker daemon..." -ForegroundColor Cyan

& minikube -p minikube docker-env --shell powershell | Invoke-Expression

# ------------------------------------------------------------
# BUILD IMAGES
# ------------------------------------------------------------

Write-Host "Building service images..." -ForegroundColor Cyan

foreach ($svc in $services) {

    $path = Join-Path $projectRoot $svc.Folder

    if (-not (Test-Path $path)) {
        throw "Service folder not found: $path"
    }

    Write-Host "Building $($svc.Tag)..." -ForegroundColor Cyan

    Push-Location $path

    docker build -t $svc.Tag .

    if ($LASTEXITCODE -ne 0) {

        Pop-Location

        throw "Docker build failed for $($svc.Tag)"
    }

    Pop-Location

    Write-Host "Built $($svc.Tag)" -ForegroundColor Green
}

# ------------------------------------------------------------
# DEPLOY SERVICES
# ------------------------------------------------------------

Write-Host "Deploying services..." -ForegroundColor Cyan

kubectl apply -f k8s/services/

# ------------------------------------------------------------
# DEPLOY GATEWAY
# ------------------------------------------------------------

Write-Host "Deploying gateway..." -ForegroundColor Cyan

kubectl apply -f k8s/gateway/

# ------------------------------------------------------------
# DEPLOY INGRESS
# ------------------------------------------------------------

Write-Host "Deploying ingress..." -ForegroundColor Cyan

kubectl apply -f k8s/ingress/

# ------------------------------------------------------------
# WAIT FOR DEPLOYMENTS
# ------------------------------------------------------------

foreach ($deployment in $deployments) {

    Write-Host "Waiting for deployment/$deployment..." -ForegroundColor Yellow

    kubectl rollout status `
        deployment/$deployment `
        -n $namespace `
        --timeout=300s
}

# ------------------------------------------------------------
# FINAL STATUS
# ------------------------------------------------------------

Write-Host ""
Write-Host "ERP environment setup completed successfully." -ForegroundColor Green
Write-Host ""

Write-Host "Useful commands:" -ForegroundColor Cyan
Write-Host "kubectl get pods -n erp"
Write-Host "kubectl get svc -n erp"
Write-Host "kubectl get ingress -n erp"
Write-Host "kubectl logs -n erp <pod-name>"
Write-Host ""

Write-Host "Available URLs:" -ForegroundColor Cyan

foreach ($hostname in $hostnames) {
    Write-Host "http://$hostname"
}