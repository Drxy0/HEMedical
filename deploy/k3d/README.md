# Running HEMedical locally on k3d

Deploys the **entire solution** to a local [k3d](https://k3d.io) cluster: the .NET
services (including 3 independent hospitals), the Angular frontend, an in-cluster SQL
Server, and an nginx Ingress.

```
http://localhost:8080
  ├─ /api/*  ─▶ client ─▶ heserver ─┬─▶ hospitalproxy-1 ─▶ hospital-1 ─┐
  │                                 ├─▶ hospitalproxy-2 ─▶ hospital-2 ─┼─▶ SQL Server (in-cluster)
  │                                 └─▶ hospitalproxy-3 ─▶ hospital-3 ─┘   (HospitalDb1/2/3)
  └─ /*      ─▶ frontend (Angular)
```

Only `client` (via `/api`) and `frontend` (via `/`) are reachable from outside; `heserver`,
the `hospitalproxy-N`/`hospital-N` pairs, and `sqlserver` are internal ClusterIP services.

Each hospital is a genuinely separate data source — its own database (on the shared SQL
Server) and its own `HospitalName`/proxy identity — so the HE Server sees three distinct
hospitals, each needing its **own** admin approval before it joins query fan-out (see
"Approve the hospitals" below). This is not replica scaling: 3 replicas of one
`hospitalproxy` would just be 3 copies of the *same* hospital behind one Service.

## Prerequisites

- **Docker Desktop running** (k3d runs the cluster inside Docker; give it ≥4 GB RAM —
  SQL Server alone wants ~2 GB)
- `k3d` and `kubectl` on PATH

## Bring it up

```powershell
.\deploy\k3d\up.ps1
```

To get query results you also need **LOINC credentials** (a free account at
[loinc.org](https://loinc.org)) — every statistics query validates its LOINC code
against `fhir.loinc.org` first. Pass them once:

```powershell
.\deploy\k3d\up.ps1 -LoincUsername 'you@example.com' -LoincPassword 'secret'
# or set $env:LOINC_USERNAME / $env:LOINC_PASSWORD beforehand
```

Without them the stack still deploys and the UI loads. Running a query then returns
`424` and the UI pops a **"LOINC credentials needed"** dialog — enter your loinc.org
account there and the query retries automatically (no redeploy needed). The `-Loinc*`
parameters above just pre-seed the same credentials so you are never prompted.

`up.ps1` does everything:
1. Creates the k3d cluster — host port **8080 → ingress :80**, Traefik disabled
2. Installs **ingress-nginx**
3. Builds the 6 images and imports them into the cluster (tag `:local`)
4. Creates the namespace and secrets (`he-keys`, `db-secret` with 3 connection strings,
   `loinc-secret`)
5. Deploys SQL Server, the backend, the 3-hospital fleet, the PlainServer, the frontend,
   and the Ingress
6. Waits for all rollouts (incl. each hospital's first EF migrate + seed)

## Approve the hospitals

Each `hospitalproxy-N` self-registers with the HE Server as **Pending** and is excluded
from query fan-out until approved. Sign in as `admin`/`admin` at `/admin` and approve
`k3d Hospital 1`, `k3d Hospital 2`, and `k3d Hospital 3` — or via the API:

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"admin"}' \
  | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')

for n in 1 2 3; do
  curl -s -X POST http://localhost:8080/api/admin/hospitals/approve \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"baseUrl\":\"http://hospitalproxy-$n:8080\"}"
done
```

Approvals persist across restarts (the HE Server's registry lives on the `heserver-data`
PVC), so you only need to do this once per cluster.

First run takes a while (image builds + SQL Server first boot). Then open
**http://localhost:8080**.

Re-run after a code change (rebuilds + re-imports + rolls out):

```powershell
.\deploy\k3d\up.ps1
```

Faster re-apply when images haven't changed:

```powershell
.\deploy\k3d\up.ps1 -SkipBuild
```

## Tear it down

```powershell
.\deploy\k3d\down.ps1                 # delete the whole cluster
.\deploy\k3d\down.ps1 -KeepCluster    # just delete the app namespace (faster next up)
```

## Files

| File | What |
|------|------|
| `up.ps1` / `down.ps1` | Bring the stack up / tear it down |
| `namespace.yaml` | The `hemedical` namespace |
| `sqlserver.yaml` | In-cluster SQL Server (ephemeral `emptyDir`), shared by all hospitals |
| `heserver-storage.yaml` | PVC for the HE Server's hospital registry (so approvals persist) |
| `backend.yaml` | client, heserver (Deployments + Services) |
| `hospitals.yaml` | 3 independent (hospital-N + hospitalproxy-N) pairs — see the file's header comment to change the count |
| `plainserver.yaml` | PlainServer, the plaintext verification twin |
| `frontend.yaml` | Angular frontend (Deployment + Service) |
| `ingress.yaml` | nginx Ingress routing `/api` → client, `/` → frontend |

Secrets aren't manifests — `up.ps1` creates them at deploy time from the committed
CKKS keys (`HEMedical/HEMedical.Client/{public,secret}.key`) and literals.

## Try it

```
http://localhost:8080/api/statistics/by-date?loincCode=4548-4&startDate=2020-01-01&endDate=2024-01-01&includeStandardDeviation=true&threshold=6.5
```

## Notes

- **DB is ephemeral** (`emptyDir`): each Hospital re-migrates and re-seeds its own
  database on every start, so a fresh DB per restart is fine. For persistence, swap the
  `emptyDir` in `sqlserver.yaml` for a PersistentVolumeClaim (k3d ships the `local-path`
  provisioner).
- **All 3 hospitals seed identical synthetic data** (`DbSeeder` uses a fixed `Random(42)`
  seed), so they're distinguishable as separate registrations/databases but not by data
  shape. Fine for exercising multi-hospital fan-out; vary the seed per instance if you
  need visibly different data per hospital.
- **Secrets are created at deploy time**, not committed. SA password is
  `HEMedical_SA_Password1` (matches `HEMedical/docker-compose.yml`).
- **LOINC creds** are required for *any* query to return data (see "Bring it up"); the
  UI and the deployment come up fine without them.
- **API host binding**: the cluster's API server is pinned to `127.0.0.1:6445`. k3d's
  default `host.docker.internal:<random>` can resolve to the LAN IP on Windows and be
  unreachable from kubectl — the fixed loopback port avoids that.
- The **PlainServer** (plaintext verification twin) *is* deployed here so the admin
  "Query Plaintext" button works — it cross-checks the encrypted results in the clear.
  It's a dev/verification tool only (never run it next to real patient data); the proxy
  exposes plaintext to it via `PlaintextVerification__Enabled=true`.

## Useful commands

```powershell
kubectl get pods -n hemedical
kubectl logs -n hemedical deploy/hospital        # watch the migrate + seed
kubectl logs -n hemedical deploy/client -f
```
