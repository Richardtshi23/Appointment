# Appointment Booking System

Customers pick a branch, a service and a date, choose from the live list of free time slots, and receive a confirmation code. After booking they can **cancel the appointment**, which frees the slot. Email/SMS confirmation and cancellation notices are **simulated** (written to the application log).

| Layer | Tech |
|-------|------|
| Front end | Angular 19 (reactive forms, standalone wizard component) |
| Back end | ASP.NET Core 8 Web API, EF Core 8 |
| Database | SQL Server (EF Core migrations applied automatically on start-up, seed data included) |
| Tests | xUnit (unit + `WebApplicationFactory` integration), Jasmine/Karma |
| Packaging | Separate Dockerfiles for API and client (nginx), orchestrated by docker-compose |

## Quick start (Docker)

```bash
docker compose up --build        # SQL Server 2022 + API + Angular client (nginx)
# optional: SA_PASSWORD=YourStr0ngPassword docker compose up --build
```

- App (Angular via nginx): http://localhost:4200 (nginx forwards `/api` to the API container)
- API directly: http://localhost:8080
- Swagger UI: http://localhost:8080/swagger
- Health: http://localhost:8080/health

Data lives in the `sql-data` volume, so bookings survive restarts. The API creates/migrates the database and seeds it on first start (the SQL Server container takes ~20s to become healthy first).

Images can also be built individually:

```bash
docker build -f Appointment.Server/Dockerfile -t appointment-api .      # context = repo root
docker build -t appointment-web ./appointment.client
```

The root `Dockerfile` is an alternative single-image build where the API serves the compiled Angular app on port 8080 (it also provides `docker build --target test .`).

## Run locally (without Docker)

Prerequisites: .NET 8 SDK (or newer), Node 20+, SQL Server (LocalDB, Express or Docker). Set `ConnectionStrings:DefaultConnection` in `Appointment.Server/appsettings.json` (for example `Server=localhost;Database=AppointmentDB;Trusted_Connection=True;TrustServerCertificate=True;`); the database is created on first run.

```bash
# Terminal 1 - API (http://localhost:5282, Swagger at /swagger)
dotnet run --project Appointment.Server --launch-profile http

# Terminal 2 - Angular dev server with proxy to the API
cd appointment.client
npm install
npx ng serve            # http://localhost:54348, /api is proxied by src/proxy.conf.js
```

`src/proxy.conf.js` targets `https://localhost:7274` (the `https` launch profile). If you use the `http` profile, set `ASPNETCORE_URLS=http://localhost:5282` before `ng serve`. Opening `Appointment.sln` in Visual Studio and pressing F5 starts both via the SPA proxy.

## Tests

```bash
# Back end (unit + integration; no SQL Server needed - tests use in-memory/temp SQLite via the test project)
SkipSpa=true dotnet test Appointment.Tests      # PowerShell: $env:SkipSpa='true'; dotnet test Appointment.Tests

# Front end (headless Chrome required)
cd appointment.client && npm run test:ci

# Back-end tests inside Docker
docker build --target test .
```

`SkipSpa=true` stops MSBuild from invoking the Angular `.esproj` build (only needed outside Visual Studio).

## API

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/branches` | List branches |
| GET | `/api/branches/{branchId}/services` | Services offered by a branch |
| GET | `/api/appointments/available-slots?branchId&serviceId&date=yyyy-MM-dd` | Free start times (`"HH:mm"`) |
| POST | `/api/appointments` | Book an appointment, returns `201` with confirmation code |
| POST | `/api/appointments/{id}/cancel` | Cancel an appointment (status becomes `Cancelled`, slot is freed). Idempotent; `404` if unknown, `400` if it has already started |
Errors use RFC 7807 `application/problem+json` with an extra flat `message` field and a `traceId`:
`400` validation / business rule, `404` unknown branch, service or appointment, `409` slot already taken, `500` generic (details only in logs).

## Design notes

- **Layering**: controllers (thin) -> services (`IAppointmentService`, `IBranchService`) -> `AppDBContext`. Notifications sit behind `INotificationService` so a real provider can replace `SimulatedNotificationService`.
- **Error handling**: `ExceptionHandlingMiddleware` converts typed exceptions (`NotFoundException`, `BusinessRuleException`, `SlotUnavailableException`) into HTTP responses and logs everything else as an error without leaking internals. Model-validation failures share the same response shape.
- **Logging**: structured `ILogger` messages for booking requests, conflicts, successful bookings, notification failures and unhandled exceptions.
- **Cancellation**: appointments are never deleted. Cancelling sets the status to `Cancelled` (history is kept), removes it from conflict/availability checks and sends a simulated cancellation notice. The UI asks for confirmation first.
- **Booking rules** (configurable under `Booking` in `appsettings.json`): 09:00-17:00, 30-minute grid, no past times, appointment must end by closing, service must be offered by the branch, one appointment at a time per branch (any service blocks the slot).
- **Time**: all times are UTC wall-clock values; opening hours are applied in UTC and the UI displays them in UTC. A production system would store a time zone per branch.
- **Concurrency**: check-then-insert is serialised with an in-process lock, which is correct for a single app instance. For multiple instances, enforce non-overlap in SQL Server (serializable transaction or `UPDLOCK/HOLDLOCK` on the branch's appointments).
- **Security**: runs as a non-root user in the container, input validated via data annotations, no secrets in the repo. Swagger is on in the container for review; set `Swagger__Enabled=false` to turn it off.

## Configuration

| Setting (env var form) | Default | Purpose |
|------------------------|---------|---------|
| `ConnectionStrings__DefaultConnection` | see `appsettings.json` (set by docker-compose in Docker) | SQL Server connection |
| `SA_PASSWORD` | `Your_strong_Passw0rd` | SQL Server `sa` password used by docker-compose |
| `Booking__OpeningTime` / `Booking__ClosingTime` | `09:00:00` / `17:00:00` | Opening hours |
| `Booking__SlotIntervalMinutes` | `30` | Slot grid |
| `Swagger__Enabled` | `false` (`true` in Docker) | Expose Swagger outside Development |
| `DisableHttpsRedirection` | `false` (`true` in Docker) | TLS is expected to terminate upstream |

## Project layout

```
Appointment.Server/   ASP.NET Core API (Controllers, Services, Middleware, Data/Migrations, Notifications) + Dockerfile
Appointment.Tests/    xUnit unit + integration tests
appointment.client/   Angular app (booking wizard, API service, models) + Dockerfile + nginx.conf
Dockerfile            optional single-image build (client + API)
docker-compose.yml    db + api + web with a persistent data volume
```

Seed data: three branches (Downtown Branch, Suburban Plaza, Westside Health Center) and three services (General Consultation 30 min, Specialized Health Assessment 60 min, Express Check-in & Lab 15 min). Downtown offers all three; Suburban Plaza offers General and Express; Westside offers General and Specialized.
