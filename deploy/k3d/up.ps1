<#
.SYNOPSIS
  Build and deploy the whole HEMedical solution to a local k3d cluster.

.DESCRIPTION
  One-shot local bring-up:
    1. Create a k3d cluster (Traefik disabled, ingress-nginx installed instead)
    2. Build the 5 app images and import them into the cluster
    3. Create the namespace + secrets (he-keys, db-secret, loinc-secret)
    4. Deploy SQL Server, the backend services, the frontend, and the Ingress
    5. Wait for everything to be ready

  Re-runnable: skips the cluster/ingress if they already exist and re-imports
  freshly built images. Requires Docker Desktop to be running.

  App is served at http://localhost:8080  (UI at /, API at /api).

.PARAMETER SkipBuild
  Reuse images already imported into the cluster (skip docker build + import).

.PARAMETER LoincUsername
.PARAMETER LoincPassword
  Credentials for the LOINC FHIR terminology server (a free loinc.org account).
  Every statistics query validates its LOINC code against fhir.loinc.org first, so
  without these the API returns 500 ("LOINC terminology server rejected our credentials").
  Default to the LOINC_USERNAME / LOINC_PASSWORD environment variables if set.
#>
[CmdletBinding()]
param(
  [switch]$SkipBuild,
  [string]$LoincUsername = $env:LOINC_USERNAME,
  [string]$LoincPassword = $env:LOINC_PASSWORD
)

# Keep our own failures fatal via explicit $LASTEXITCODE checks + throw, but do NOT
# let a native tool's stderr (k3d and docker log progress to stderr) abort the script.
$ErrorActionPreference = 'Continue'

$Cluster   = 'hemedical'
$HostPort  = 8080
$ApiPort   = 6445          # fixed loopback API port (see cluster section)
$Registry  = 'hemedical'   # local image name prefix (no real registry)
$Tag       = 'local'
$IngressNginxVersion = 'controller-v1.11.3'

$Root = (Get-Item $PSScriptRoot).Parent.Parent.FullName   # repo root
$K3d  = $PSScriptRoot

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# --- 0. sanity: Docker must be up ---------------------------------------------
Step "Checking Docker is running"
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker does not appear to be running. Start Docker Desktop and retry." }

$images = @(
  @{ name = 'hemedical-client';        dockerfile = 'HEMedical/HEMedical.Client/Dockerfile';        context = 'HEMedical' },
  @{ name = 'hemedical-heserver';      dockerfile = 'HEMedical/HEMedical.HEServer/Dockerfile';      context = 'HEMedical' },
  @{ name = 'hemedical-hospitalproxy'; dockerfile = 'HEMedical/HEMedical.HospitalProxy/Dockerfile'; context = 'HEMedical' },
  @{ name = 'hemedical-hospital';      dockerfile = 'HEMedical/HEMedical.Hospital/Dockerfile';      context = 'HEMedical' },
  @{ name = 'hemedical-plainserver';   dockerfile = 'HEMedical/HEMedical.PlainServer/Dockerfile';   context = 'HEMedical' },
  @{ name = 'hemedical-frontend';      dockerfile = 'client-frontend/Dockerfile';                   context = 'client-frontend' }
)
$refs = $images | ForEach-Object { "$Registry/$($_.name):$Tag" }

# --- 1. build images ----------------------------------------------------------
# Built BEFORE the cluster exists: the dotnet/Angular builds are memory-hungry,
# and on a small Docker VM running them alongside a live k3s node can OOM-kill
# the cluster. No cluster running here means the builds get the whole VM.
if ($SkipBuild) {
  Step "Skipping image build (-SkipBuild)"
} else {
  foreach ($img in $images) {
    $ref = "$Registry/$($img.name):$Tag"
    Step "Building $ref"
    docker build -t $ref -f (Join-Path $Root $img.dockerfile) (Join-Path $Root $img.context)
    if ($LASTEXITCODE -ne 0) { throw "docker build failed for $ref" }
  }
}

# --- 2. cluster ---------------------------------------------------------------
$existing = k3d cluster list -o json | ConvertFrom-Json | Where-Object { $_.name -eq $Cluster }
if ($existing) {
  Step "k3d cluster '$Cluster' already exists - reusing it"
} else {
  # --api-port binds the k3s API to a fixed loopback port. Without this, k3d
  # writes the kubeconfig server as https://host.docker.internal:<random>, which
  # on some Windows setups resolves to the LAN IP and is unreachable from kubectl.
  Step "Creating k3d cluster '$Cluster' (API 127.0.0.1:$ApiPort, host $HostPort -> ingress :80, Traefik disabled)"
  k3d cluster create $Cluster `
    --api-port "127.0.0.1:$ApiPort" `
    --port "$($HostPort):80@loadbalancer" `
    --k3s-arg "--disable=traefik@server:0" `
    --wait
  if ($LASTEXITCODE -ne 0) { throw "k3d cluster create failed" }
}

# Point kubectl at this cluster and force the server to loopback (belt-and-braces:
# some k3d versions still write host.docker.internal even with --api-port).
kubectl config use-context "k3d-$Cluster" | Out-Null
kubectl config set-cluster "k3d-$Cluster" --server="https://127.0.0.1:$ApiPort" | Out-Null

Step "Waiting for the cluster API to be reachable"
$deadline = (Get-Date).AddSeconds(90)
do {
  kubectl get nodes *> $null
  if ($LASTEXITCODE -eq 0) { break }
  Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)
if ($LASTEXITCODE -ne 0) { throw "Cluster API not reachable at https://127.0.0.1:$ApiPort" }

# --- 3. import images into the cluster ----------------------------------------
# Import right after the node is ready (before ingress-nginx): the image-import
# tools node is happiest against a quiet, freshly-created cluster.
Step "Importing images into k3d cluster '$Cluster'"
k3d image import @refs -c $Cluster
if ($LASTEXITCODE -ne 0) { throw "k3d image import failed" }

# --- 4. ingress-nginx ---------------------------------------------------------
$hasIngress = kubectl get ns ingress-nginx --ignore-not-found
if ($hasIngress) {
  Step "ingress-nginx already installed - skipping"
} else {
  Step "Installing ingress-nginx ($IngressNginxVersion)"
  kubectl apply -f "https://raw.githubusercontent.com/kubernetes/ingress-nginx/$IngressNginxVersion/deploy/static/provider/cloud/deploy.yaml"
  Step "Waiting for the ingress-nginx controller to be ready"
  kubectl wait --namespace ingress-nginx `
    --for=condition=Available deployment/ingress-nginx-controller `
    --timeout=180s
}

# --- 4. namespace + secrets ---------------------------------------------------
Step "Applying namespace"
kubectl apply -f (Join-Path $K3d 'namespace.yaml')

# One connection string per hospital instance, each naming its own database
# (HospitalDb1/2/3) on the shared sqlserver pod — see hospitals.yaml.
function ConnStr($dbName) {
  "Server=sqlserver;Database=$dbName;User Id=sa;Password=HEMedical_SA_Password1;TrustServerCertificate=True;Encrypt=False;"
}

Step "Creating/updating secrets (he-keys, db-secret, loinc-secret)"
# he-keys carries the ~370 KB CKKS key pair. `kubectl apply` would stash a copy in
# the last-applied-configuration annotation and blow the 256 KB annotation limit,
# so create it directly (delete-then-create keeps the step re-runnable).
kubectl delete secret he-keys -n hemedical --ignore-not-found | Out-Null
kubectl create secret generic he-keys -n hemedical `
  --from-file=public.key=(Join-Path $Root 'HEMedical/HEMedical.Client/public.key') `
  --from-file=secret.key=(Join-Path $Root 'HEMedical/HEMedical.Client/secret.key')

kubectl create secret generic db-secret -n hemedical `
  --from-literal=connectionString1=(ConnStr 'HospitalDb1') `
  --from-literal=connectionString2=(ConnStr 'HospitalDb2') `
  --from-literal=connectionString3=(ConnStr 'HospitalDb3') `
  --from-literal=saPassword=HEMedical_SA_Password1 `
  --dry-run=client -o yaml | kubectl apply -f -

# LOINC creds: required for the terminology check every query runs. Pass them via
# -LoincUsername/-LoincPassword or the LOINC_USERNAME/LOINC_PASSWORD env vars; empty
# still deploys, but queries will 500 until real credentials are supplied.
if ([string]::IsNullOrEmpty($LoincUsername)) {
  Write-Host "    (no LOINC credentials given - queries will fail until you set them; see README)" -ForegroundColor Yellow
}
kubectl create secret generic loinc-secret -n hemedical `
  --from-literal=username=$LoincUsername `
  --from-literal=password=$LoincPassword `
  --dry-run=client -o yaml | kubectl apply -f -

# --- 5. workloads -------------------------------------------------------------
Step "Deploying SQL Server"
kubectl apply -f (Join-Path $K3d 'sqlserver.yaml')

Step "Provisioning HEServer registry storage"
kubectl apply -f (Join-Path $K3d 'heserver-storage.yaml')

Step "Deploying backend services"
kubectl apply -f (Join-Path $K3d 'backend.yaml')

Step "Deploying hospital fleet (3 independent hospitals)"
kubectl apply -f (Join-Path $K3d 'hospitals.yaml')

Step "Deploying PlainServer (plaintext verification twin)"
kubectl apply -f (Join-Path $K3d 'plainserver.yaml')

Step "Deploying frontend"
kubectl apply -f (Join-Path $K3d 'frontend.yaml')

Step "Applying Ingress"
kubectl apply -f (Join-Path $K3d 'ingress.yaml')

# --- 6. wait ------------------------------------------------------------------
Step "Waiting for rollouts (this includes the first EF migration + seed on Hospital)"
$deployments = 'sqlserver','heserver',
  'hospital-1','hospitalproxy-1','hospital-2','hospitalproxy-2','hospital-3','hospitalproxy-3',
  'plainserver','client','frontend'
foreach ($d in $deployments) {
  kubectl rollout status deployment/$d -n hemedical --timeout=300s
}

Write-Host "`nAll set. Open the app at:" -ForegroundColor Green
Write-Host "  http://localhost:$HostPort/            (Angular UI)" -ForegroundColor Green
Write-Host "  http://localhost:$HostPort/api/...     (Client API)" -ForegroundColor Green
Write-Host "`nExample:" -ForegroundColor Green
Write-Host "  http://localhost:$HostPort/api/statistics/by-date?loincCode=4548-4&startDate=2020-01-01&endDate=2024-01-01&includeStandardDeviation=true&threshold=6.5"
