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

| Service         | URL                    |
|-----------------|------------------------|
| HEServer        | http://localhost:5000  |
| HospitalProxy   | http://localhost:5001  |
| Hospital        | http://localhost:5002  |
| Client          | http://localhost:5003  |

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

| Service         | URL                    |
|-----------------|------------------------|
| HEServer        | http://localhost:5000  |
| HospitalProxy   | http://localhost:5001  |
| Hospital        | http://localhost:5002  |
| Client          | http://localhost:5003  |
| SQL Server      | localhost:1433         |

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

- Note: Age range is inclusive

```
http://localhost:5003/api/statistics/by-date?measurementType=HbA1c&startDate=2020-01-01&endDate=2024-01-01
http://localhost:5003/api/statistics/by-age?measurementType=BloodPressure&startAge=20&endAge=40
```

