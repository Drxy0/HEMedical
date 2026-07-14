## Running Backend Locally

### Prerequisites

- Visual Studio 2026
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

### 1. Database

Start a local SQL Server instance and ensure the connection string in `HEMedical/HEMedical.Hospital/appsettings.json` points to it:

```json
"Database": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=HospitalDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
}
```

### 2. Apply Migrations

```bash
cd HEMedical/HEMedical.Hospital
dotnet ef database update
```

### 3. Start Each Service

Run the following projects:

| Service         | URL                    | Notes                                  |
|-----------------|------------------------|----------------------------------------|
| Client          | http://localhost:5000  | Holds the HE keys; the API to query    |
| HEServer        | http://localhost:5010  | Aggregates encrypted results           |
| PlainServer     | http://localhost:5020  | Verification twin (optional, test only)|
| Hospital        | http://localhost:5040  | Mock FHIR data source                  |
| HospitalProxy   | http://localhost:5100  | Encrypts inside the hospital boundary  |

This can be done in two ways using Visual Studio:
1) Right-click on project name -> Debug -> Start new instance/Star without debugging
2) Right-click on solution name -> Configure startup projects -> check Multiple startup projects -> set all except HEMedical.Shared action to 'Start' or 'Start without debugging' 
---

## Running Backend with Docker

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1. Build and Start

From the `HEMedical/` folder (where `docker-compose.yml` is located):

```bash
docker compose up --build
```

Started services:

| Service         | URL                    | Notes                                  |
|-----------------|------------------------|----------------------------------------|
| Client          | http://localhost:5000  | Holds the HE keys; the API to query    |
| HEServer        | http://localhost:5001  | Aggregates encrypted results           |
| HospitalProxy   | http://localhost:5002  | Encrypts inside the hospital boundary  |
| Hospital        | http://localhost:5003  | Mock FHIR data source                  |
| PlainServer     | http://localhost:5004  | Verification twin (plaintext, enabled via the `PlaintextVerification` flag) |
| SQL Server      | localhost:1433         |                                        |

### 2. Stop

```bash
docker compose down
```

To also remove the database volume:

```bash
docker compose down -v
```

### Notes

- The SQL Server SA password is `HEMedical_SA_Password1`.

## Example endpoints testable with Postman

Queries go to the Client API and identify measurements by LOINC code (`loincCode`, plus `componentLoincCode` for values recorded inside panels):

```
# HbA1c average over a date range, with standard deviation and the prevalence at or above 6.5
http://localhost:5000/api/statistics/by-date?loincCode=4548-4&startDate=2020-01-01&endDate=2024-01-01&includeStandardDeviation=true&threshold=6.5

# Systolic blood pressure (panel 85354-9, component 8480-6) for ages 20-40
http://localhost:5000/api/statistics/by-age?loincCode=85354-9&componentLoincCode=8480-6&startAge=20&endAge=40

# Breakdown of the HbA1c average per 10-year age group
http://localhost:5000/api/statistics/breakdown-by-age?loincCode=4548-4&startAge=0&endAge=79&bucketSize=10&includeStandardDeviation=true

# Frequency histogram: six bins of width 1 starting at 4
http://localhost:5000/api/statistics/histogram-by-date?loincCode=4548-4&startDate=2014-01-01&endDate=2018-12-31&binStart=4&binWidth=1&binCount=6

# Verification twin of any query (requires the PlainServer + the PlaintextVerification flag)
http://localhost:5000/api/verification/by-date?loincCode=4548-4&startDate=2020-01-01&endDate=2024-01-01
```
### Notes: 
- Age range is inclusive
- For local deployment HEMedical.Client URL is: https://localhost:5000/api
- For Docker deployment HEMedical.Client URL is: http://localhost:5000/api



