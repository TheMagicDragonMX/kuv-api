# kuv-api

## Database configuration

The API uses PostgreSQL through Entity Framework Core. Keep the connection string out of source control.

For local development, set it as a user secret:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your PostgreSQL connection URL>"
```

For deployment, set the `ConnectionStrings__DefaultConnection` environment variable instead.

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