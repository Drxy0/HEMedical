# Deploying HEMedical to Azure (AKS + Terraform + GitHub Actions)

This deploys the whole system to **Azure Kubernetes Service**: the four .NET services,
the Angular frontend, an Azure SQL database, and a Key Vault, fronted by an nginx Ingress.

```
Internet
  │
  ▼
Ingress (nginx, public IP)
  ├─ /api/*  ─────────────▶  client        (public API)
  └─ /*      ─────────────▶  frontend       (Angular, nginx)
                                 client ─▶ heserver ─▶ hospitalproxy ─▶ hospital ─▶ Azure SQL
                                                                        └─▶ hapi.fhir.org (fallback)
                                 client ─▶ fhir.loinc.org (LOINC verification)
```

Only `client` and `frontend` are reachable from outside; `heserver`, `hospitalproxy`
and `hospital` are internal `ClusterIP` services.

## Layout

| Path | What |
|------|------|
| `deploy/terraform/` | Infrastructure: RG, ACR, AKS, Azure SQL, Key Vault, k8s namespace/secrets, ingress-nginx |
| `deploy/k8s/` | App manifests: Deployments, Services, Ingress (image refs are `envsubst` placeholders) |
| `client-frontend/Dockerfile` + `nginx.conf` | Builds and serves the Angular app |
| `.github/workflows/deploy.yml` | CI/CD: build 5 images in ACR, roll out to AKS |

## Prerequisites

- An Azure subscription and the **az** CLI (`az login`)
- **terraform** ≥ 1.6 and **kubectl**
- The repo on GitHub (for the CI/CD workflow)

## 1. Provision infrastructure (one time)

```bash
cd deploy/terraform
cp terraform.tfvars.example terraform.tfvars   # then edit it
terraform init
terraform fmt && terraform validate
terraform plan
terraform apply
```

`terraform apply` creates everything **and** loads the CKKS key pair and secrets into the
cluster. Supply secrets via `terraform.tfvars` or environment variables:

```bash
export TF_VAR_sql_admin_password='Strong!Passw0rd'
export TF_VAR_loinc_username='your-loinc-user'   # only needed for custom LOINC codes
export TF_VAR_loinc_password='your-loinc-pass'
```

Point kubectl at the new cluster (Terraform prints the exact command):

```bash
az aks get-credentials --resource-group hemedical-rg --name hemedical-aks
```

## 2. Wire up GitHub Actions (one time)

The workflow logs in with **OIDC** (no stored passwords). Create an app registration with a
federated credential for this repo, grant it **Contributor** + **AcrPush** on the resource
group, then add three repo secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

(If `prefix` in Terraform isn't `hemedical`, update the `env:` block at the top of
`.github/workflows/deploy.yml` to match.)

## 3. Deploy

Push to `main` (or run the workflow manually). It builds the five images in ACR tagged with
the commit SHA, applies the manifests, and waits for the rollouts.

Get the public IP and open the app:

```bash
kubectl get svc -n ingress-nginx ingress-nginx-controller \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

### Deploying by hand (without CI)

```bash
export IMAGE_REGISTRY=hemedicalacr.azurecr.io
export IMAGE_TAG=latest
az acr build -r hemedicalacr -t hemedical-client:$IMAGE_TAG        -f HEMedical/HEMedical.Client/Dockerfile        HEMedical
# …repeat for heserver, hospitalproxy, hospital, and the frontend…
envsubst < deploy/k8s/backend.yaml  | kubectl apply -f -
envsubst < deploy/k8s/frontend.yaml | kubectl apply -f -
kubectl apply -f deploy/k8s/ingress.yaml
```

## Teardown

```bash
cd deploy/terraform && terraform destroy
```

## Notes & caveats

- **CKKS keys.** The committed `public.key`/`secret.key` are loaded into the `he-keys`
  Kubernetes Secret and mounted into the **Client only** (the key owner). Proxies receive
  the public key over the network: each proxy registers itself with the HE Server
  (`POST /api/hospitals/register`) and gets the current public key in the response; the
  Client publishes the key to the HE Server at startup and every minute. Every statistics
  request carries an `X-HE-Key-Fingerprint` header, so a stale key anywhere fails loudly
  (409) instead of decrypting to garbage. See `key-distribution-and-discovery.pdf` in the
  repo root for the full protocol, diagrams, and caveats.
- **Hospital discovery.** The HE Server fans out to hospitals that have registered within
  the TTL (default 180 s; heartbeat every 60 s) plus any statically configured
  `HospitalsProxies:Urls` kept as a fallback. New hospitals join by deploying a proxy
  pointed at the HE Server — no HE Server redeploy.
- **Secrets.** DB connection string and LOINC credentials live in Key Vault (source of truth)
  and are mirrored into Kubernetes Secrets by Terraform. The AKS Key Vault CSI driver is
  enabled, so you can later mount straight from Key Vault instead of the mirrored secrets.
- **TLS.** The Ingress serves plain HTTP. For a real domain, add cert-manager + a DNS record
  and a `tls:` block. `UseHttpsRedirection` in the services is a no-op here (no HTTPS port is
  configured), so internal HTTP calls are unaffected.
- **Azure SQL** is serverless and auto-pauses after 60 min idle; the first request after a
  pause incurs a few-seconds cold start.
- **Mock Hospital.** In a real deployment the `hospital` service is replaced by actual
  hospital FHIR endpoints; the Proxy just needs their base URLs.
- Terraform here uses local state. For team use, uncomment the `backend "azurerm"` block in
  `versions.tf` and create the state storage account first.
