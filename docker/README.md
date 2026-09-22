# Docker database

From the repository root, start a PostgreSQL database initialized to the current Entity Framework migration state:

```powershell
docker compose up -d
```

The main database is available at `localhost:5432` with the same connection settings as `src\Hotdesks.Booking.Api\appsettings.json`.

The `test-postgres` service runs a separate PostgreSQL instance at `localhost:5433`. Its PostgreSQL process listens on container port `5433`, matching the published `5433:5433` port mapping.

| Setting | Value |
|---|---|
| Database | `test-hotdesks-booking` |
| Username | `test-postgres` |
| Password | `test-postgres` |

Each database includes the `InitialCreate` schema and EF migration-history entry.

The initialization script in `docker\postgres\init` runs only when Docker creates a service's named volume. To recreate only the test database:

```powershell
docker compose rm --stop --force test-postgres
docker volume rm hotdesks-booking_test-postgres-data
docker compose up -d test-postgres
```

To discard both local databases and recreate them from the schema scripts:

```powershell
docker compose down -v
docker compose up -d
```
