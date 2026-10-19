# kuv-api

## Database configuration

The API uses PostgreSQL through Entity Framework Core. Keep the connection string out of source control.

For local development, set it as a user secret:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your PostgreSQL connection URL>"
```

For deployment, set the `ConnectionStrings__DefaultConnection` environment variable instead.

## Token configuration

Token lifetimes are set in the `Jwt` section of `appsettings.json`:

- `Jwt:AccessTokenMinutes`: minutes an access token is valid (default `15`).
- `Jwt:RefreshTokenDays`: days a refresh token is valid (default `7`).

## EF Core commands

Restore the local EF CLI tool once:

```powershell
dotnet tool restore
```

Create a migration after changing entity classes:

```powershell
dotnet ef migrations add InitialCreate
```

Apply pending migrations:

```powershell
dotnet ef database update
```

To completely reset the database during development, this permanently deletes its data:

```powershell
dotnet ef database drop --force
dotnet ef database update
```

## Error handling

Expected errors are thrown as `KuvException`s (`Exceptions/`), which carry an HTTP status code and a message. Any layer (services, controllers) can throw one; controllers do not catch them.

| Exception               | Status |
|-------------------------|--------|
| `BadRequestException`   | 400    |
| `UnauthorizedException` | 401    |
| `ForbiddenException`    | 403    |
| `NotFoundException`     | 404    |
| `ConflictException`     | 409    |

```csharp
throw new ConflictException("El nombre de usuario o correo ya está registrado.");
```

The global `KuvExceptionHandler` (registered in `Program.cs`) catches them and responds with the exception's status and a body of `{ "message": "..." }`. Any other exception is logged and returned as a generic `500`, without internal details.

Methods never return `null` to signal failure; they throw instead. Nullable reference types are enabled project-wide in `kuv-api.csproj`.
