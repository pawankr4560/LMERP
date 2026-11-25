# Northstar Lending Admin

An ASP.NET Core 8 and Angular 19 starter for a loan management admin workspace.

## Current implementation

- Responsive admin shell with dashboard, customer, loan, repayment, collection, reports, users, and settings screens.
- Lazy loaded Angular routes, standalone components, reactive forms, a bearer token interceptor, and an auth guard.
- SQL Server persistence for customers and loan applications.
- Authenticated APIs for dashboard summary, customers, and loan applications. Mutating customer and loan actions are restricted by role.
- Development seed records for previewing the customer, loan, and dashboard APIs.
- Preview data for repayment, collection, report, user, settings, and some dashboard visualizations until those API modules are implemented.

## Server layers

- `LMERP.Domain` contains the loan and customer entities.
- `LMERP.Application` contains request/response DTOs, use-case services, and repository abstractions.
- `LMERP.Infrastructure` contains Entity Framework Core, SQL Server persistence, Identity, and repository implementations.
- `LMERP.Server` is the API/presentation layer: controllers, authentication, validation, middleware, and dependency wiring.

All API endpoints use `BaseResponse<T>` with the same envelope:

```json
{
  "success": true,
  "message": "Request completed.",
  "data": {},
  "errors": []
}
```

Validation, authorization, not-found, conflict, and exception responses use the same fields and include error messages in `errors` where applicable.

## Configure local access

The repository contains no bootstrap credentials or machine-specific database connection strings. Configure local access outside tracked files with user secrets or environment variables.

Set the LocalDB connection string and a JWT signing key using user secrets (the signing key shown here is generated locally):

```powershell
dotnet user-secrets set "ConnectionStrings:LoanDb" "Server=(localdb)\MSSQLLocalDB;Database=NorthstarLending;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project .\LMERP.Server\LMERP.Server.csproj
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) --project .\LMERP.Server\LMERP.Server.csproj
```

Create an administrator using local user secrets (replace the example values):

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project .\LMERP.Server\LMERP.Server.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<a-new-unique-password>" --project .\LMERP.Server\LMERP.Server.csproj
```

The password must be at least 12 characters and include upper and lower case letters, a number, and a symbol. User secrets stay outside the repository. In Development, the server generates an ephemeral JWT signing key if one is not configured. Environment variable names use double underscores, for example `ConnectionStrings__LoanDb`, `Jwt__SigningKey`, `BootstrapAdmin__Email`, and `BootstrapAdmin__Password`. For production, configure a stable signing key and database connection in a secret store and supply bootstrap admin values only when an initial administrator needs to be created.

## Run

From the solution folder:

```powershell
dotnet run --project .\LMERP.Server\LMERP.Server.csproj
```

The ASP.NET Core SPA proxy starts the Angular development server. Open the frontend URL shown in the console. The Swagger UI is available from the development server at `/swagger`.

For a standalone client build:

```powershell
cd .\lmerp.client
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadlessNoGpu
```

The headless launcher disables GPU acceleration for environments where Chrome cannot initialize its GPU process.

## API overview

- `POST /api/auth/login`
- `GET /api/dashboard/summary`
- `GET|POST /api/customers`
- `GET|PUT|DELETE /api/customers/{id}`
- `GET|POST /api/loans`
- `GET /api/loans/{id}`

The server applies pending EF Core migrations at startup with `Database.MigrateAsync()`. Create new schema changes as migrations and review/apply them before deploying to production.
