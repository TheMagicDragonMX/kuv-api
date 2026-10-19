# Coding conventions for AI agents

Follow these rules whenever you write or modify code in this project.

## 1. Never use `null` as an error or logic signal

- `null` is only allowed when the absence of a value is a legitimate, expected state of the data (e.g. an optional field).
- When an operation cannot complete (duplicate user, invalid state, missing required entity, etc.), **throw an exception** instead of returning `null`, `false`, or a sentinel value.
- Service methods return the non-nullable result type (`Task<User>`, not `Task<User?>`).

## 2. Error handling: `KuvException` hierarchy

- All expected errors are `KuvException`s ([Exceptions/KuvException.cs](Exceptions/KuvException.cs)), which carry a `StatusCode` and a `Message`.
- Do **not** create a new exception class per error. Throw one of the existing utility exceptions with a message:
  `BadRequestException` (400), `UnauthorizedException` (401), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` (409).
  Add a new subclass in `Exceptions/` only when a status code is missing.
- Services throw; they do not catch to translate errors into return values.
- Controllers contain no try/catch and no error-mapping code. The global `KuvExceptionHandler` (registered in `Program.cs` via `AddExceptionHandler` / `UseExceptionHandler`) is the single place that catches exceptions:
  - `KuvException` -> its `StatusCode` with `{ "message": "..." }`.
  - Any other `Exception` -> logged, `500` with a generic message (never leak internal details).

## 3. Nullable reference types are enabled project-wide

- `<Nullable>enable</Nullable>` is set in `kuv-api.csproj`.
- Do **not** add `#nullable enable` to individual files.
- Do not silence nullable warnings with `!` unless the value is guaranteed by construction.

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
- Properties that are not set by callers (e.g. the database-generated `Id`) and nullable properties (`string?`) are not `required`.

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
