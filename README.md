# Appointment Booking System

Customers pick a branch, a service and a date, choose from the live list of free time slots, and receive a confirmation code. Email/SMS confirmation is **simulated** (written to the application log).

| Layer | Tech |
|-------|------|
| Front end | Angular 19 (reactive forms, standalone wizard component) |
| Back end | ASP.NET Core 8 Web API, EF Core 8 |
| Database | SQLite (migrations applied automatically on start-up, seed data included) |
| Tests | xUnit (unit + `WebApplicationFactory` integration), Jasmine/Karma |
| Packaging | Multi-stage Dockerfile; the API serves the compiled Angular app |

## Quick start (Docker)

```bash
docker compose up --build
# or without compose
docker build -t appointment-booking .
docker run -p 8080:8080 -v appointment-data:/data appointment-booking
```

- App: http://localhost:8080
- Swagger UI: http://localhost:8080/swagger
- Health: http://localhost:8080/health

The SQLite file lives in the `/data` volume, so bookings survive restarts.

## Run locally (without Docker)

Prerequisites: .NET 8 SDK (or newer), Node 20+.

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
# Back end (unit + integration, uses temp/in-memory SQLite)
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

Errors use RFC 7807 `application/problem+json` with an extra flat `message` field and a `traceId`:
`400` validation / business rule, `404` unknown branch or service, `409` slot already taken, `500` generic (details only in logs).

## Design notes

- **Layering**: controllers (thin) -> services (`IAppointmentService`, `IBranchService`) -> `AppDBContext`. Notifications sit behind `INotificationService` so a real provider can replace `SimulatedNotificationService`.
- **Error handling**: `ExceptionHandlingMiddleware` converts typed exceptions (`NotFoundException`, `BusinessRuleException`, `SlotUnavailableException`) into HTTP responses and logs everything else as an error without leaking internals. Model-validation failures share the same response shape.
- **Logging**: structured `ILogger` messages for booking requests, conflicts, successful bookings, notification failures and unhandled exceptions.
- **Booking rules** (configurable under `Booking` in `appsettings.json`): 09:00-17:00, 30-minute grid, no past times, appointment must end by closing, service must be offered by the branch, one appointment at a time per branch (any service blocks the slot).
- **Time**: all times are UTC wall-clock values; opening hours are applied in UTC and the UI displays them in UTC. A production system would store a time zone per branch.
- **Concurrency**: check-then-insert is serialised with an in-process lock, which is correct for the single-instance SQLite deployment. For multiple instances, move to PostgreSQL/SQL Server and enforce non-overlap in the database (exclusion constraint or serializable transaction).
- **Security**: runs as a non-root user in the container, input validated via data annotations, no secrets in the repo. Swagger is on in the container for review; set `Swagger__Enabled=false` to turn it off.

## Configuration

| Setting (env var form) | Default | Purpose |
|------------------------|---------|---------|
| `ConnectionStrings__Default` | `Data Source=appointments.db` (`/data/appointments.db` in Docker) | SQLite file |
| `Booking__OpeningTime` / `Booking__ClosingTime` | `09:00:00` / `17:00:00` | Opening hours |
| `Booking__SlotIntervalMinutes` | `30` | Slot grid |
| `Swagger__Enabled` | `false` (`true` in Docker) | Expose Swagger outside Development |
| `DisableHttpsRedirection` | `false` (`true` in Docker) | TLS is expected to terminate upstream |

## Project layout

```
Appointment.Server/   ASP.NET Core API (Controllers, Services, Middleware, Data/Migrations, Notifications)
Appointment.Tests/    xUnit unit + integration tests
appointment.client/   Angular app (booking wizard, API service, models)
Dockerfile            client build -> server build/test -> runtime image
docker-compose.yml    one-command run with a persistent data volume
```

Seed data: three branches (Cape Town CBD, Johannesburg Sandton, Durban Umhlanga) and four services; Umhlanga offers only the 30-minute services.
