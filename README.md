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
- Swagger: http://localhost:8080/swagger
- Health check: http://localhost:8080/health

SQL Server takes around 20 seconds to be ready, so the API may need a moment on the first start. Data is kept in the `sql-data` volume. To change the database password, set `SA_PASSWORD` before running compose.

There is also a root `Dockerfile` that builds one image where the API serves the Angular app. `docker build --target test .` runs the back-end tests inside Docker.

## Running locally

You need the .NET 8 SDK, Node 20+ and a SQL Server instance (LocalDB, Express or a container). Put your connection string in `Appointment.Server/appsettings.json` under `ConnectionStrings:DefaultConnection`, for example:

```
Server=localhost;Database=AppointmentDB;Trusted_Connection=True;TrustServerCertificate=True;
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
```

`SkipSpa` stops MSBuild from building the Angular project during the test run. The back-end tests don't need SQL Server.

## API

| Method | Route | What it does |
|--------|-------|--------------|
| GET | `/api/branches` | List branches |
| GET | `/api/branches/{branchId}/services` | Services a branch offers |
| GET | `/api/appointments/available-slots?branchId&serviceId&date=yyyy-MM-dd` | Free start times as `HH:mm` |
| POST | `/api/appointments` | Book an appointment (201 with the confirmation code) |
| POST | `/api/appointments/{id}/cancel` | Cancel an appointment |
| GET | `/api/appointments/lookup?code&email` | Find an appointment by confirmation code and the email it was booked with |

Errors come back as `application/problem+json` with a `message` and a `traceId`. 400 is for validation or a broken booking rule, 404 for an unknown branch, service or appointment, and 409 when the slot is already taken.

## Notes on how it works

- **Booking rules**: 09:00 to 17:00, 30 minute slots, nothing in the past, and the appointment has to finish before closing. The branch must offer the service, and a branch can only have one appointment at a time. Hours and slot size can be changed under `Booking` in `appsettings.json`.
- **Cancelling**: customers can cancel right after booking, or later from the "Manage my booking" page (`/manage`) by entering their confirmation code and email. The simulated confirmation email contains a `/manage?code=...` link that prefills the code. A wrong code and a wrong email give the same "not found" error. Appointments are never deleted, they are marked `Cancelled` so there is a history. A cancelled appointment doesn't block the slot any more. Appointments that have already started can't be cancelled.
- **Form validation**: the booking form shows messages for a missing or badly formatted email and phone number, and the service dropdown says "Select a branch first" until a branch is picked.
- **Errors and logging**: `ExceptionHandlingMiddleware` turns the custom exceptions into HTTP responses, and anything unexpected is logged and returned as a generic 500. Logging uses structured `ILogger` messages.
- **Time zones**: everything is UTC, including opening hours. A real system would store a time zone for each branch.
- **Concurrency**: the check for a clashing appointment and the insert are protected by an in-process lock, which is fine for a single instance. With several instances you would need to enforce this in SQL Server instead.
- **Swagger** is on in Docker for easy reviewing. Set `Swagger__Enabled=false` to turn it off.

## Configuration

| Setting | Default | Purpose |
|---------|---------|---------|
| `ConnectionStrings__DefaultConnection` | see `appsettings.json` | SQL Server connection (set by compose in Docker) |
| `SA_PASSWORD` | `Your_strong_Passw0rd` | SQL Server password used by compose |
| `Booking__OpeningTime` / `Booking__ClosingTime` | `09:00:00` / `17:00:00` | Opening hours |
| `Booking__SlotIntervalMinutes` | `30` | Slot size |
| `Swagger__Enabled` | `false` (`true` in Docker) | Show Swagger outside Development |
| `DisableHttpsRedirection` | `false` (`true` in Docker) | Docker sits behind plain HTTP |

## Project layout

```
Appointment.Server/   the API (controllers, services, middleware, data, notifications)
Appointment.Tests/    xUnit unit and integration tests
appointment.client/   Angular app, nginx config and its Dockerfile
Dockerfile            optional single-image build
docker-compose.yml    database, API and web client
```

Seed data: three branches (Downtown Branch, Suburban Plaza, Westside Health Center) and three services (General Consultation 30 min, Specialized Health Assessment 60 min, Express Check-in & Lab 15 min). Downtown offers all three, Suburban Plaza offers General and Express, and Westside offers General and Specialized.
