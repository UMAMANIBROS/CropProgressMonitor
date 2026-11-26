# CropProgress Monitor: Real-Time Estimation of Agricultural Growth Stages

**Course:** Modern Web Programming Tools and Techniques (`INT402`)  
**Tech Stack:** ASP.NET Core 8.0 MVC, C#, Entity Framework Core, Microsoft SQL Server, ASP.NET Core Identity, Bootstrap 5, Chart.js, Open-Meteo APIs, xUnit, Docker.

---

## Architecture Overview

```text
Presentation (Razor Views + Bootstrap 5 + Chart.js)
        ↓
Application (MVC Controllers + ViewModels + Anti-Forgery + Validation)
        ↓
Services (WeatherService, GrowthStageService, AdvisoryService, HistoryService)
        ↓
Data Access (EF Core ApplicationDbContext + Migrations)
        ↓
Microsoft SQL Server  <--->  Open-Meteo (Geocoding, Archive, Forecast APIs)
```

### Core Growing Degree Day (GDD) Model
For each day from `SowingDate` through today:
$$\text{GDD}_{\text{daily}} = \max\left(0,\ \frac{T_{\max} + T_{\min}}{2} - T_{\text{base}}\right)$$
- Daily values are summed into `AccumulatedGdd` and compared against `CropStages` thresholds.
- Next-stage dates are projected using the 7-day Open-Meteo forecast, then extrapolated via the trailing 7-day average daily GDD.
- Daily estimates are persisted in `GrowthEstimates` without duplicate entries per `(FieldCropId, EstimateDate)`.
- **Disclaimer:** Seeded crop thresholds and estimates are illustrative thermal-time approximations and must be validated against local agronomic references.

---

## Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer SDK with .NET 8 runtime)
- Microsoft SQL Server (SQL Server LocalDB on Windows, or Docker SQL Server)
- Optional: `dotnet-ef` CLI (`dotnet tool install --global dotnet-ef`)

---

## Local Setup & Run Instructions

### 1. Configure Connection String (`appsettings.json`)
Default configuration for Windows SQL Server LocalDB:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CropProgressMonitorDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```
If using SQL Server in Docker, update `DefaultConnection` (or set `ConnectionStrings__DefaultConnection`):
```text
Server=localhost,1433;Database=CropProgressMonitorDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
```

### 2. Apply Entity Framework Core Migrations
From the repository root (`CropProgressMonitor`):
```bash
dotnet ef database update --project CropProgressMonitor/CropProgressMonitor.csproj
```
*(Note: The app also calls `context.Database.MigrateAsync()` and `DbInitializer.InitializeAsync()` on startup, which creates the database, applies pending migrations, and seeds initial data automatically.)*

### 3. Run Unit Tests (xUnit)
```bash
dotnet test CropProgressMonitor.Tests/CropProgressMonitor.Tests.csproj
```

### 4. Run the Application
```bash
dotnet run --project CropProgressMonitor/CropProgressMonitor.csproj
```
Open the URL printed in the console (typically `https://localhost:5xxx` or `http://localhost:5xxx`).

### Seeded Demo Accounts
- **Farmer Account:** `farmer@cropprogress.local` / `Farmer@123`
- **Admin Account:** `admin@cropprogress.local` / `Admin@123` (accesses `/Admin` to add crops, stage thresholds, and multilingual advisories)

---

## Docker Build & Run Guide

### Build Image
```bash
docker build -t cropprogress-monitor .
```

### Run Container (connected to external SQL Server)
```bash
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=CropProgressMonitorDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --name cropprogress-app \
  cropprogress-monitor
```
Then visit `http://localhost:8080`.

---

## Deploying to Render
1. Push this repository to GitHub/GitLab.
2. Provision a managed Microsoft SQL Server instance (e.g., Azure SQL Database Free Tier, AWS RDS, or Railway/cloud SQL Server) and copy its ADO.NET connection string.
3. On [Render](https://render.com), click **New +** -> **Web Service** and connect your repository.
4. Choose **Runtime: Docker** (Render will detect the root `Dockerfile`).
5. Add Environment Variables under **Environment**:
   - `ConnectionStrings__DefaultConnection`: `<your cloud SQL Server connection string>`
   - `ASPNETCORE_ENVIRONMENT`: `Production`
   - `OpenMeteo__GeocodingUrl`: `https://geocoding-api.open-meteo.com/v1/search`
   - `OpenMeteo__ForecastUrl`: `https://api.open-meteo.com/v1/forecast`
   - `OpenMeteo__ArchiveUrl`: `https://archive-api.open-meteo.com/v1/archive`
6. Deploy! The `ENTRYPOINT` automatically binds to Render's `$PORT` and runs EF migrations + seeding on startup.

---

## Report Screenshot Checklist (Chapter 8)
Capture the following screens after running the app for your INT402 project report:
1. **Home Page** (`/`) — Hero banner, GDD formula card, feature highlights, Login/Register buttons.
2. **Farmer Registration** (`/Account/Register`) — Name, email, location, preferred language, password.
3. **Farmer Login** (`/Account/Login`) — Email/password login with demo credentials card.
4. **Farmer Dashboard** (`/Dashboard`) — Field cards showing current weather, stage badge, and animated GDD progress bar.
5. **Add Field & Crop** (`/Fields/Create`) — Crop dropdown, sowing date, and Open-Meteo geocoding location autocomplete.
6. **Growth Stage Timeline** (`/Growth/Timeline`) — Completed/current/upcoming visual stage sequence, next-stage date estimate, and cumulative GDD Chart.js graph.
7. **Weather Page** (`/Weather`) — Current conditions banner and 7-day temperature & rainfall Chart.js visualization.
8. **Advisory Page** (`/Advisory`) — Stage-specific Water/Nutrient/Risk cards, rainfall field notes, and EN/HI/PA language toggle.
9. **History Page** (`/History`) — Multi-field GDD-over-time comparison chart and saved `GrowthEstimates` table.
10. *(Optional)* **Admin Page** (`/Admin`) — Managing crops, GDD thresholds, and advisories without code changes.
