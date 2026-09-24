# Hotdesks Booking API

ASP.NET Core API for managing hotdesks and their reservations. It uses PostgreSQL
to enforce that an active hotdesk cannot have overlapping reservations.

## Run the service

### Prerequisites

- .NET 10 SDK
- Docker Desktop running

From the repository root, start PostgreSQL:

```powershell
docker compose up -d
```

The connection string checked into the repository has its password redacted.
Set the development connection string for the current PowerShell session, then
start the API:

```powershell
$env:ConnectionStrings__HotdesksBooking = "Host=localhost;Port=5432;Database=hotdesks-booking;Username=postgres;Password=postgres"
dotnet run --project .\src\Hotdesks.Booking.Api
```

The service listens on `http://localhost:5168`. Stop it with `Ctrl+C`. To stop
the database later, run `docker compose down`.

### Call the API from PowerShell

The commands below use `Invoke-RestMethod`, PowerShell's native HTTP client.
In Windows PowerShell, `curl` is an alias for `Invoke-WebRequest`; prefer
`Invoke-RestMethod` here because it automatically deserializes JSON responses.
Do not use `curl -Method ...` in PowerShell 7, where `curl` commonly resolves
to `curl.exe` instead.

Set the base URL and create a hotdesk:

```powershell
$api = "http://localhost:5168"

$hotdesk = Invoke-RestMethod -Method Post -Uri "$api/api/hotdesks" `
  -ContentType "application/json" `
  -Body (@{ name = "Window desk"; isAvailable247 = $true } | ConvertTo-Json)

$hotdesk
Invoke-RestMethod -Method Get -Uri "$api/api/hotdesks"
```

Create a user before adding a reservation:

```powershell
$user = Invoke-RestMethod -Method Post -Uri "$api/api/users" `
  -ContentType "application/json" `
  -Body (@{ name = "Example User" } | ConvertTo-Json)
```

Create, list, edit, cancel, and inspect the history of a reservation. The
`$reservation` response supplies both IDs needed by subsequent requests:

```powershell
$from = (Get-Date).ToUniversalTime().AddHours(1).ToString("o")
$to = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")

$reservation = Invoke-RestMethod -Method Post -Uri "$api/reservation/add" `
  -ContentType "application/json" `
  -Body (@{
    userId = $user.id
    hotdeskId = $hotdesk.id
    from = $from
    to = $to
  } | ConvertTo-Json)

Invoke-RestMethod -Method Get -Uri "$api/reservation"
Invoke-RestMethod -Method Get -Uri "$api/reservation/$($reservation.versionId)/history"

$editedFrom = (Get-Date).ToUniversalTime().AddHours(3).ToString("o")
$editedTo = (Get-Date).ToUniversalTime().AddHours(4).ToString("o")
$editedReservation = Invoke-RestMethod -Method Post -Uri "$api/reservation/$($reservation.id)/edit" `
  -ContentType "application/json" `
  -Body (@{
    hotdeskId = $hotdesk.id
    from = $editedFrom
    to = $editedTo
  } | ConvertTo-Json)

Invoke-RestMethod -Method Post -Uri "$api/reservation/$($editedReservation.id)/cancel"
Invoke-RestMethod -Method Delete -Uri "$api/api/hotdesks/$($hotdesk.id)"
```

Deleting a hotdesk is a soft delete: it removes the desk from the list of
available desks without deleting its record.

## Run tests

Start the separate test database, set its connection string in the current
PowerShell session, and run the integration tests:

```powershell
docker compose up -d test-postgres
$env:ConnectionStrings__HotdesksBooking = "Host=localhost;Port=5433;Database=test-hotdesks-booking;Username=test-postgres;Password=test-postgres"
dotnet test .\tests\Hotdesks.Booking.Api.Tests\Hotdesks.Booking.Api.Tests.csproj
```

The tests use the shared `test-postgres` database and clean up records they
create. Do not run multiple test commands against that database concurrently.

## Key decisions, trade-offs, and limitations

- **PostgreSQL is the source of truth for conflicting bookings.** The API first
  checks for overlapping active reservations to return a helpful `409`, and a
  PostgreSQL exclusion constraint enforces the same rule under concurrent
  writes. This requires PostgreSQL and the `btree_gist` extension rather than
  a database-agnostic implementation.
- **Reservations are versioned, not overwritten.** Editing or cancelling a
  current reservation creates a new version and retains the prior row. The
  normal list endpoint returns current versions; the history endpoint returns
  all versions for a `versionId`. This preserves an audit trail at the cost of
  extra rows and explicit version handling.
- **Hotdesk deletion is a soft delete.** This preserves reservation history
  and prevents new reservations on the disabled desk, but a disabled desk is
  not exposed by the list endpoint.
- **Schema creation is Docker initialization, not runtime migration.** The
  initialization SQL runs only when Docker creates a named volume. When adding
  migrations, recreate the local database volume or apply the migration by a
  separate deployment process.
- **This is a focused API, not a complete product.** It has no authentication,
  authorization, user listing, updates, or deletion, pagination, OpenAPI/Swagger
  UI, or production deployment configuration. The development passwords are
  only for the local Docker containers and must not be used in production.
