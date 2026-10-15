# Ekomas

A small e-commerce REST API built with ASP.NET Core (.NET 10), EF Core and SQL Server. It covers the parts of a shop backend that are easy to get subtly wrong: concurrent stock updates, payment webhooks, ownership-based authorization, and background cleanup.

This is a personal learning and portfolio project. Payments run against the Xendit **sandbox** only.

## Features

- **Catalog**: product CRUD, paging, search and price filtering, image upload and download
- **Orders**: place orders with multiple lines, with the unit price snapshotted at purchase time
- **Stock safety**: stock is decremented with a single conditional `UPDATE ... WHERE Stock >= qty` inside a transaction, so concurrent buyers can't oversell
- **Auth**: register and login with salted password hashing, JWT bearer tokens, `Admin` / `Customer` roles
- **Authorization**: customers can only read their own orders and customer record (queries are scoped by owner; others get 404)
- **Payments**: Xendit invoice per order, signed-token webhook, idempotent status updates, late and duplicate payment handling
- **Background work**: a hosted service expires unpaid orders and returns their stock
- **Reports**: orders CSV export streamed in keyset batches (constant memory, fast first byte)
- **Errors**: global exception handler returning `ProblemDetails` (409 for constraint conflicts, 502 for payment provider failures, generic 500)

## Tech stack

| Layer | Choice |
|---|---|
| Framework | ASP.NET Core Web API (controllers), .NET 10 |
| Data | EF Core 10, SQL Server 2022 (Docker) |
| Auth | JWT bearer, `PasswordHasher<T>` |
| Payments | Xendit Invoice API (sandbox) via typed `HttpClient` |
| Tests | xUnit, `WebApplicationFactory`, a Python concurrency script |

## Architecture

```
Client
  -> Kestrel
  -> middleware (exception handler, authentication, authorization)
  -> controller            HTTP concerns: binding, status codes
  -> service               business rules (OrderService, PaymentService)
  -> EF Core / DbContext   -> SQL Server
                 \
                  -> IPaymentGateway (XenditGateway) -> HttpClient -> Xendit API

Xendit -> POST /webhooks/xendit -> token check -> PaymentService -> DbContext

ExpiredOrderSweeper (BackgroundService) -> DbContext   (periodic)
```

## Getting started

### Prerequisites

- .NET 10 SDK
- Docker (for SQL Server)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`
- Optional: Python 3 (concurrency script), a Xendit sandbox account and an HTTPS tunnel such as `cloudflared` or `ngrok` (webhooks)

### 1. Start SQL Server

```
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<choose-a-strong-password>" \
  -p 1433:1433 --name ekomas-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Configure secrets

Secrets are kept out of the repo using .NET user-secrets:

```
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=Ekomas;User Id=sa;Password=<same-password>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "<random string, at least 32 characters>"
dotnet user-secrets set "Xendit:SecretKey" "<sandbox secret key>"
dotnet user-secrets set "Xendit:CallbackToken" "<webhook verification token from the Xendit dashboard>"
dotnet user-secrets set "Xendit:Currency" "MYR"
```

Non-secret defaults (`Jwt:Issuer`, `Jwt:Audience`) live in `appsettings.json`. Uploaded files go to `uploads/` by default (`Storage:Path` to change).

### 3. Create the database and run

```
dotnet ef database update
dotnet run
```

The API listens on the URL printed at startup (by default `http://localhost:5221`).

### 4. Create the first admin

There is intentionally no endpoint for this. Register a user, then:

```sql
UPDATE Users SET Role = 'Admin' WHERE Email = 'you@example.com';
```

Log in again to get a token that carries the new role.

## Configuration

| Key | Where | Purpose |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets / env | SQL Server connection |
| `Jwt:Key` | user-secrets / env | HMAC signing key (secret) |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` | token validation |
| `Xendit:SecretKey` | user-secrets / env | Xendit API key (sandbox) |
| `Xendit:CallbackToken` | user-secrets / env | webhook verification token |
| `Xendit:Currency` | user-secrets / env | invoice currency |
| `Storage:Path` | optional | upload folder, default `uploads` |

## API overview

| Method | Path | Access | Notes |
|---|---|---|---|
| POST | `/auth/register` | public | `name`, `email`, `password`; creates a customer profile |
| POST | `/auth/login` | public | returns `{ token }` |
| GET | `/auth/me` | any user | claims of the caller |
| GET | `/products` | public | `page`, `limit`, `search`, `minPrice`; returns `{ items, page, pageSize, totalCount }` |
| GET | `/products/{id}` | public | |
| POST / PUT / DELETE | `/products`, `/products/{id}` | Admin | DELETE returns 409 if the product appears in orders |
| PUT | `/products/{id}/image` | Admin | multipart `file`; JPEG/PNG/WebP, max 2 MB, type checked by content |
| GET | `/products/{id}/image` | public | supports range requests |
| GET | `/customers/{id}` | owner or Admin | |
| POST | `/orders` | customer | customer comes from the token; stock reserved atomically |
| GET | `/orders/{id}` | owner or Admin | 404 for other customers' orders |
| POST | `/orders/{id}/pay` | owner or Admin | creates (or returns) the Xendit invoice link |
| POST | `/webhooks/xendit` | Xendit | verified by `x-callback-token` |
| GET | `/reports/orders.csv` | Admin | streamed export |

## Payment flow

1. Customer places an order. Stock is reserved and the order is `Unpaid`.
2. `POST /orders/{id}/pay` creates a Xendit invoice with `external_id = order-{id}`. The order becomes `Pending` and the checkout URL is returned. Calling it again returns the same URL.
3. Xendit calls `/webhooks/xendit`. The handler verifies the token (constant-time comparison), finds the order by `external_id`, checks the paid amount against the order total, and marks it `Paid`. Duplicate deliveries change nothing.
4. If the invoice is never paid, `ExpiredOrderSweeper` marks the order `Expired` and returns the stock.
5. If a paid event arrives for an already-expired order, the handler tries to re-reserve the stock. If that is no longer possible, the order is marked `RefundRequired` and an error is logged.

To test webhooks locally, expose the app with a tunnel and set the invoice callback URL in the Xendit dashboard to `https://<tunnel>/webhooks/xendit`.

## Design notes

- **Atomic conditional updates** decide races (stock reservation, order expiry, late payment). The database is the referee instead of load-modify-save.
- **No blind retries on invoice creation.** Xendit did not de-duplicate invoices by `external_id` in testing, so create-invoice is not retried automatically.
- **Amounts come from the order's items**, never from the request.
- **Single provider class** (`XenditGateway` behind `IPaymentGateway`) so the payment provider can be replaced in one place.
- **Ownership is part of the query**, so other users' data is indistinguishable from missing data.
- **No DB transaction is held across the external HTTP call.**

## Tests

```
dotnet test tests/Ekomas.Tests
```

API tests start the app in memory against a throwaway SQL Server database (created from the real migrations and dropped afterwards), so Docker must be running.

Concurrency check against a running instance, with stock set to 10:

```
python scripts/concurrency_test.py --email <customer-email> --password <password> --product-id <id>
```

Expected: exactly as many `201` responses as units in stock, and stock ends at 0.

## Known limitations

- Sandbox payments only; no refunds flow beyond marking `RefundRequired`
- Background work is in-process, so a restart loses nothing (state is in the database) but there is no durable job queue or outbox
- Image storage is the local disk behind `IFileStorage` (a cloud storage implementation would be a drop-in)
- Test coverage is small (auth and registration); the ownership and webhook paths are covered by manual and script tests
- No rate limiting, no CI pipeline yet
