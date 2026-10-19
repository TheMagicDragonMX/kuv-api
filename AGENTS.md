# Coding conventions for AI agents

Follow these rules whenever you write or modify code in this project.

## 1. Never use `null` as an error or logic signal

- `null` is only allowed when the absence of a value is a legitimate, expected state of the data (e.g. an optional field).
- When an operation cannot complete (duplicate user, invalid state, missing required entity, etc.), **throw an exception** instead of returning `null`, `false`, or a sentinel value.
- Service methods return the non-nullable result type (`Task<User>`, not `Task<User?>`).
- Use specific exception types placed in `Exceptions/` (e.g. `UserAlreadyExistsException`) rather than generic `Exception`.

## 2. Controllers are the only place that catch exceptions

- Services and other lower layers throw; they do not catch in order to translate errors into return values.
- Every controller action catches the exceptions its service calls can throw and maps them to the proper HTTP response (e.g. `UserAlreadyExistsException` -> `409 Conflict`).
- Error responses use the shape `{ "message": "..." }`.

## 3. Nullable reference types are enabled project-wide

- `<Nullable>enable</Nullable>` is set in `kuv-api.csproj`.
- Do **not** add `#nullable enable` to individual files.
- Do not silence nullable warnings with `!` unless the value is guaranteed by construction.
