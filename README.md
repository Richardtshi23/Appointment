# Appointment Booking

A small appointment booking app. You pick a branch, a service and a date, choose one of the free time slots and get a confirmation code back. You can also cancel afterwards, which frees the slot again.

Emails and SMS messages are not really sent. They are just written to the application log.

Built with an Angular 19 front end, an ASP.NET Core 8 Web API with EF Core, and SQL Server. Migrations run on start-up and the database is seeded automatically.

## Running with Docker

```bash
docker compose up --build
```

- App: http://localhost:4200 (nginx serves Angular and forwards `/api` to the API)
- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger (off by default, set `Swagger__Enabled=true` on the api service to use it)
- Health check: http://localhost:8080/health

SQL Server takes around 20 seconds to be ready, so the API may need a moment on the first start. Data is kept in the `sql-data` volume. The API connects with a limited `appuser` login, not `sa`. The passwords default to development values, so set `SA_PASSWORD` and `APP_DB_PASSWORD` before running compose anywhere real. If you ran an older version of this project, run `docker compose down -v` first, because the migrations were squashed into one.

There is also a root `Dockerfile` that builds one image where the API serves the Angular app. `docker build --target test .` runs the back-end tests inside Docker.

## Running locally

You need the .NET 8 SDK, Node 20+ and a SQL Server instance (LocalDB, Express or a container). Set your connection string with user secrets so it stays out of source control:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=AppointmentDB;Trusted_Connection=True;TrustServerCertificate=True" --project Appointment.Server
```

Then start the API and the client in two terminals:

```bash
dotnet run --project Appointment.Server --launch-profile http

cd appointment.client
npm install
npx ng serve
```

The client runs on http://localhost:54348 and proxies `/api` using `src/proxy.conf.js`. That file points at the https profile (`https://localhost:7274`), so if you use the http profile set `ASPNETCORE_URLS=http://localhost:5282` first. Opening `Appointment.sln` in Visual Studio and pressing F5 also works.

## Tests

```bash
# back end
$env:SkipSpa='true'; dotnet test Appointment.Tests      # PowerShell
SkipSpa=true dotnet test Appointment.Tests              # bash

# front end (needs Chrome)
cd appointment.client
npm run test:ci

# browser test with Playwright, API is stubbed so only the dev server is needed
npm run e2e
```

`SkipSpa` stops MSBuild from building the Angular project during the test run. The back-end tests don't need SQL Server.

## API

| Method | Route | What it does |
|--------|-------|--------------|
| GET | `/api/branches` | List branches |
| GET | `/api/branches/{branchId}/services` | Services a branch offers |
| GET | `/api/appointments/available-slots?branchId&serviceId&date=yyyy-MM-dd` | Free start times as `HH:mm` |
| POST | `/api/appointments` | Book an appointment (201 with the confirmation code) |
| POST | `/api/appointments/cancel` | Cancel an appointment. Body: `confirmationCode` and `email` |
| POST | `/api/appointments/lookup` | Find an appointment. Body: `confirmationCode` and `email` |

Errors come back as `application/problem+json` with a `message` and a `traceId`. 400 is for validation or a broken booking rule, 404 for an unknown branch, service or appointment, 409 when the slot is already taken, and 429 when lookup or booking is called too often (10 lookups or 20 bookings a minute per client).

## Notes on how it works

- **Booking rules**: 09:00 to 17:00, 30 minute slots, nothing in the past, and the appointment has to finish before closing. The branch must offer the service, and a branch can only have one appointment at a time. Hours and slot size can be changed under `Booking` in `appsettings.json`.
- **Cancelling**: customers can cancel right after booking, or later from the "Manage my booking" page (`/manage`) by entering their confirmation code and email. The simulated confirmation email contains a `/manage?code=...` link that prefills the code. Cancelling needs both the code and the email, and a wrong code and a wrong email give the same "not found" error. Both are sent in a POST body so the email stays out of URLs and logs. Appointments are never deleted, they are marked `Cancelled` so there is a history. A cancelled appointment doesn't block the slot any more. Appointments that have already started can't be cancelled.
- **Form validation**: the booking form shows messages for a missing or badly formatted email and phone number, and the service dropdown says "Select a branch first" until a branch is picked.
- **Errors and logging**: `ExceptionHandlingMiddleware` turns the custom exceptions into HTTP responses, and anything unexpected is logged and returned as a generic 500. Logging uses structured `ILogger` messages, and emails and phone numbers are masked in the simulated notifications.
- **Time zones**: times are stored in UTC, but opening hours (09:00 to 17:00) are applied in South African time, set by `Booking:TimeZoneId` (default `Africa/Johannesburg`). Slots and confirmations are shown in SA time, and the client sends bookings with a `+02:00` offset.
- **Concurrency**: bookings for a branch are serialised with an in-process lock plus a SQL Server application lock (`sp_getapplock`) inside a transaction, so it also holds with several API instances. There is a test that fires parallel bookings at one slot. The lock is per branch, so other branches are not blocked.
- **Seed data** comes from `HasData` in `AppDBContext` and is applied by the single `InitialCreate` migration.
- **Known limits**: a branch can only hold one appointment at a time (no staff or capacity model), and there is one time zone for the whole app rather than one per branch.

## Configuration

| Setting | Default | Purpose |
|---------|---------|---------|
| `ConnectionStrings__DefaultConnection` | see `appsettings.json` | SQL Server connection (set by compose in Docker) |
| `SA_PASSWORD` | development default | SQL Server `sa` password, only used by the db and db-init containers |
| `APP_DB_PASSWORD` | development default | Password of the limited `appuser` login the API uses |
| `Booking__OpeningTime` / `Booking__ClosingTime` | `09:00:00` / `17:00:00` | Opening hours |
| `Booking__SlotIntervalMinutes` | `30` | Slot size |
| `Swagger__Enabled` | `false` | Show Swagger outside Development |
| `DisableHttpsRedirection` | `false` (`true` in Docker) | Docker sits behind plain HTTP |

## Project layout

```
Appointment.Server/   the API (controllers, services, middleware, data, notifications)
Appointment.Tests/    xUnit unit and integration tests
appointment.client/   Angular app, nginx config and its Dockerfile
Dockerfile            optional single-image build
docker-compose.yml    database, db-init, API and web client
.github/workflows/  CI: back-end tests, front-end tests and build, docker build
```

Seed data: three branches (Downtown Branch, Suburban Plaza, Westside Health Center) and three services (General Consultation 30 min, Specialized Health Assessment 60 min, Express Check-in & Lab 15 min). Downtown offers all three, Suburban Plaza offers General and Express, and Westside offers General and Specialized.
