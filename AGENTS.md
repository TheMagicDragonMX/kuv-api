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
