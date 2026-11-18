# Coding conventions for AI agents

Follow these rules whenever you write or modify code in this project.

## 1. Never use `null` as an error or logic signal

- `null` is only allowed when the absence of a value is a legitimate, expected state of the data (e.g. an optional field).
- When an operation cannot complete (duplicate user, invalid state, missing required entity, etc.), **throw an exception** instead of returning `null`, `false`, or a sentinel value.
- Service methods return the non-nullable result type (`Task<User>`, not `Task<User?>`).

## 2. Error handling: `KuvException` hierarchy

- All expected errors are `KuvException`s ([Models/Exceptions/KuvException.cs](Models/Exceptions/KuvException.cs)), which carry a `StatusCode` and a `Message`.
- Do **not** create a new exception class per error. Throw one of the existing utility exceptions with a message:
  `BadRequestException` (400), `UnauthorizedException` (401), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` (409).
  Add a new subclass in `Models/Exceptions/` only when a status code is missing.
- Services throw; they do not catch to translate errors into return values.
- Controllers contain no try/catch and no error-mapping code. The global `KuvExceptionHandler` (registered in `Program.cs` via `AddExceptionHandler` / `UseExceptionHandler`) is the single place that catches exceptions:
  - `KuvException` -> its `StatusCode` with `{ "message": "..." }`.
  - Any other `Exception` -> logged, `500` with a generic message (never leak internal details).

## 3. Nullable reference types are enabled project-wide

- `<Nullable>enable</Nullable>` is set in `kuv-api.csproj`.
- Do **not** add `#nullable enable` to individual files.
- Do not silence nullable warnings with `!` unless the value is guaranteed by construction.
- Navigation properties to a required relationship (non-nullable foreign key) are non-nullable and initialized with `= null!` (e.g. `public User Follower { get; set; } = null!;`). They are not `required`, and use sites never need `!`. Only navigations to optional relationships (nullable foreign key) are typed `?`.
- Declare a navigation property right after its foreign key property (e.g. `Follower` right after `FollowerId`), not grouped at the end of the class.

## 3b. Entity base classes

- They live in `Models/Entities/Base/` (namespace `kuv_api.Models.Entities.Base`), apart from the real entities, and each lives on its own: `BaseEntity` gives the `Id` key; `ICreatedAt` and `IUpdatedAt` are interfaces for `CreatedAt` and `UpdatedAt`. C# allows only one base class, so an entity extends `BaseEntity` (if it has an `Id`) and implements the interfaces it needs, declaring those properties itself (`User` and `Creation` have both; `Follow`, which has a composite key and no `Id`, only `ICreatedAt`).
- `KuvDbContext.SaveChanges` stamps the times: `CreatedAt` on insert, `UpdatedAt` on insert and update. Never set them by hand.

## 3c. Creations

- The database never holds the program of a creation. It lives in `creations/{id}/program.json` of the file store, next to its video; everything about those files goes through `CreationStorage`. Services never touch `File`/`Directory` directly: they use `IFileStore` (disk on the server, the server's WebDAV folder on a local API), see `README.md` > File storage.
- The format of `program.json` is `Schemas/program-file.schema.json`. Every program file is validated by `ProgramFileValidator` before it is stored. Per-block fields are validated by the client engine, not by the schema. A change to the program format changes the schema, `kuv-client` and `kuv-cube` together.

## 4. Document what you create

- Always write XML doc tags on multiple lines (opening tag, content, closing tag each on its own `///` line), even when the content is short. Never use the one-line form `/// <summary>Text</summary>`.
- Add XML doc comments (`/// <summary>`) to every new public class, method and exception. Use `<exception cref="...">` on methods that throw `KuvException`s, and `<param>` / `<returns>` when they add information.
- When you add or change a feature, convention or endpoint, update `README.md` in the same change.
- Keep this file in sync when a convention changes.

## 5. Method naming: no `Async` suffix

- Do not add the `Async` suffix to methods we create, even when they return `Task` / `Task<T>` (e.g. `Register`, not `RegisterAsync`).
- Methods that implement or override a framework/library contract (e.g. `IExceptionHandler.TryHandleAsync`) keep the name the contract requires.

## 6. Required properties

- In every class we create (entities, models, DTOs), a property gets the `required` keyword unless its value can legitimately be `null`.
- Do not initialize non-nullable properties with placeholders such as `= string.Empty` just to silence nullable warnings; use `required` instead.
- Properties that are not set by callers (e.g. the database-generated `Id`, or `CreatedAt` / `UpdatedAt`, which `KuvDbContext` stamps on save) and nullable properties (`string?`) are not `required`.

## 7. Blank lines between declarations

- Separate every declaration from the one before it with a blank line: properties, fields, methods, constructors, nested types. This applies even when a declaration is short or has an XML doc comment.
- The XML doc comment belongs to the declaration below it, so the blank line goes **before** the comment, never between the comment and its declaration.
- The first member inside a type does not need a blank line after the opening brace.

```csharp
public class User : BaseEntity
{
    /// <summary>
    /// Unique username used to log in.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Unique email address.
    /// </summary>
    public required string Email { get; set; }
}
```

## 8. Lambda parameter names in collection queries

- The parameter of a lambda passed to a collection or query method (`AnyAsync`, `FirstOrDefaultAsync`, `Where`, `Select`, etc.) is named after the element being queried, in singular and camelCase: `user` for `Users`, `order` for `Orders`.
- Do not use generic names such as `item`, `x`, `e` or `u`.

```csharp
await dbContext.Users.AnyAsync(user => user.Username == username);
```

## 9. Formatting of lambdas in method calls

- The lambda starts on the same line as the opening parenthesis of the call: `AnyAsync(user =>`, never `AnyAsync(` followed by the lambda on the next line.
- A lambda with a single condition is a one-liner, including the closing parenthesis.
- A lambda with more than one condition breaks after `=>`. Each condition goes on its own indented line, the boolean operator (`||`, `&&`) stays at the **end** of the line (never at the start of the next one), and the closing parenthesis goes on its own line, aligned with the start of the statement.

```csharp
var user = await dbContext.Users.SingleOrDefaultAsync(user => user.Username == username);

var alreadyExists = await dbContext.Users.AnyAsync(user =>
    user.Username.ToLower() == username.ToLower() ||
    user.Email.ToLower() == email.ToLower()
);
```

## 10. Closing parenthesis of multi-line method calls

- When the arguments of a method or constructor call are split across multiple lines, the closing parenthesis goes on its own line, aligned with the start of the statement. Never leave it at the end of the last argument.
- This also applies to the multi-condition lambdas described in rule 9.

```csharp
var token = new JwtSecurityToken(
    issuer: configuration["Jwt:Issuer"],
    audience: configuration["Jwt:Audience"],
    claims: claims,
    expires: expiresAt,
    signingCredentials: credentials
);
```

## 11. Where type files live

- Files that only define classes/records that get instantiated live in `Models/`, one subfolder per kind, and the namespace follows the folder: `Models/Entities` (database entities), `Models/Exceptions` (the `KuvException` hierarchy), `Models/Requests` (request DTOs), `Models/Messages` (socket messages), `Models/Responses` (response DTOs), `Models/Files` (files read from the file store).
- Classes with behavior (services, the exception handler, controllers, data access) stay outside `Models/`.
