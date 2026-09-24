# Hotdesks Booking API

ASP.NET Core API for managing hotdesks and reservations. PostgreSQL prevents
overlapping active reservations for the same hotdesk.

## Run the service

### Prerequisites

- .NET 10 SDK for Windows
- Docker and a WSL distribution with `curl`

From Windows PowerShell, start the databases in WSL:

```powershell
wsl.exe sh -lc "cd /mnt/d/repos/hotdesks-booking && docker compose up -d"
```

The checked-in connection string has its password redacted. Set the local
development connection string for the current PowerShell session and start the
API:

```powershell
$env:ConnectionStrings__HotdesksBooking = "Host=localhost;Port=5432;Database=hotdesks-booking;Username=postgres;Password=postgres"
$env:ASPNETCORE_URLS = "http://0.0.0.0:5168"
dotnet run --no-launch-profile --project .\src\Hotdesks.Booking.Api
```

The API listens on port `5168` on the Windows host. Keep this process running
and open a second terminal to call it. Stop it with `Ctrl+C`.

## Call the API with curl from WSL

Open WSL from a second Windows PowerShell terminal:

```powershell
wsl.exe
```

Run the following commands in the WSL shell. They use the standard `curl`
binary, not PowerShell's `curl` alias. The first command obtains the Windows
host's WSL gateway address, where the API is running.

```bash
api=http://$(ip route show default | awk '{print $3}'):5168

user_json=$(curl --silent --show-error --fail-with-body \
  --request POST "$api/api/users" \
  --header "Content-Type: application/json" \
  --data '{"name":"Example User"}')
printf '%s\n' "$user_json"
user_id=$(printf '%s' "$user_json" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')

hotdesk_json=$(curl --silent --show-error --fail-with-body \
  --request POST "$api/api/hotdesks" \
  --header "Content-Type: application/json" \
  --data '{"name":"Window desk","isAvailable247":true}')
printf '%s\n' "$hotdesk_json"
hotdesk_id=$(printf '%s' "$hotdesk_json" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')

curl --silent --show-error --fail-with-body \
  --request POST "$api/reservation/add" \
  --header "Content-Type: application/json" \
  --data "{\"userId\":\"$user_id\",\"hotdeskId\":\"$hotdesk_id\",\"from\":\"2026-10-01T09:00:00Z\",\"to\":\"2026-10-01T10:00:00Z\"}"
printf '\n'

curl --silent --show-error --fail-with-body \
  --request POST "$api/reservation/add" \
  --header "Content-Type: application/json" \
  --data "{\"userId\":\"$user_id\",\"hotdeskId\":\"$hotdesk_id\",\"from\":\"2026-10-01T10:00:00Z\",\"to\":\"2026-10-01T11:00:00Z\"}"
printf '\n'

curl --silent --show-error --fail-with-body "$api/reservation"
printf '\n'
```

The two reservation intervals are adjacent rather than overlapping, so both
requests should return `201 Created`. Change the second reservation's `from`
time to `2026-10-01T09:30:00Z` to verify the expected `409 Conflict`.

## Run tests

Start the separate test database in WSL, then run the tests from Windows
PowerShell:

```powershell
wsl.exe sh -lc "cd /mnt/d/repos/hotdesks-booking && docker compose up -d test-postgres"
$env:ConnectionStrings__HotdesksBooking = "Host=localhost;Port=5433;Database=test-hotdesks-booking;Username=test-postgres;Password=test-postgres"
dotnet test .\tests\Hotdesks.Booking.Api.Tests\Hotdesks.Booking.Api.Tests.csproj
```

The tests use a shared database and clean up their own records. Do not run
multiple test commands against it concurrently.

## Key decisions, trade-offs, and limitations

- **Database-enforced booking conflicts.** The API returns a helpful `409` for
  an overlap, while a PostgreSQL exclusion constraint guarantees the rule under
  concurrent writes. This depends on PostgreSQL and its `btree_gist` extension.
- **Versioned reservations.** Editing or cancelling creates a new version
  rather than overwriting the prior record. The list endpoint returns current
  versions and the history endpoint returns all versions for a reservation.
- **Soft-deleted hotdesks.** Deleting a desk disables it, preserving
  reservation history while preventing new reservations.
- **Manual schema lifecycle.** Docker initialization SQL runs only for a new
  named volume. Apply later migrations through a deployment process or
  recreate the local volume.
- **Product scope.** The API has no authentication, authorization, user
  listing/updates/deletion, pagination, OpenAPI UI, or production deployment
  configuration. The local Docker credentials are not production credentials.
