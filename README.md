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

## Sessions and Google sign-in

Everything under `/session` is public (no token needed). Whichever way a user gets in, they end up with the API's own access and refresh tokens (`TokenResponse`), so the rest of the API does not know how the user signed in.

| Endpoint | What it does |
|---|---|
| `POST /session/register` | Creates an account with a username, email, age and password. |
| `POST /session/login` | `{ username, password }`. The `username` field accepts the username **or the email**, ignoring case. A Google-only account answers `401` with a message saying to use Google. |
| `POST /session/refresh` | Exchanges a refresh token for a new pair (each refresh token works once). |
| `POST /session/google` | `{ idToken }` with the ID token Google gave the browser. Answers `{ status: "signedIn", tokens }`, or `{ status: "needsProfile", signupToken, email }` when no user has that Google account yet. |
| `POST /session/google/complete` | `{ signupToken, age, username }`. Creates the account and answers the tokens. |
| `GET /session/username-available?username=` | `{ available }`, ignoring case. |

How `POST /session/google` decides (`Services/GoogleAuthService.cs`):

1. The ID token is checked with Google (signature, issuer, expiry, and that its audience is `Google:ClientId`). Google must also have verified the email.
2. A user with that Google id (`Users.GoogleId`) signs in.
3. Otherwise a user with the same email (ignoring case) **is linked**: their `GoogleId` is saved and they sign in. If that user is already linked to a different Google account the answer is `409`.
4. Otherwise nothing is created yet. The API answers `needsProfile` with a `signupToken`: a short-lived JWT (15 minutes) signed by the API that remembers the verified Google id and email. Its audience differs from the access tokens', so neither can be used as the other. The client asks for the age (`0`-`150`) and a unique username (`3`-`50` characters, compared ignoring case), then calls `/session/google/complete`.

Accounts created with Google have no password (`Users.Hashword` is null) and can only sign in with Google. Linked accounts keep their password.

Configuration: set `Google:ClientId` (environment variable `Google__ClientId`) to the OAuth client ID of the Google Cloud project; for local development put `Google__ClientId=...` in `.env`. Deployment fills it from the `GOOGLE_CLIENT_ID` repository variable (`Deploy/kuv-api.env`). Without it, `/session/google` answers `500` and password login keeps working. The same client ID goes in the web app's `environments/*.ts`. No client secret is needed: the API only verifies tokens, it never talks to Google on the user's behalf.

The migration `AddGoogleSignIn` adds `Users.GoogleId` (unique) and makes `Users.Hashword` nullable; apply it with `dotnet ef database update`.

## CORS

Browsers may call the API only from the origins listed in `Cors:AllowedOrigins` (`appsettings.json`: `https://kuv.lol`; `appsettings.Development.json` adds `http://localhost:4200`). Any header and method is allowed. To add an origin, edit that list or set `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`... as environment variables.

## Cube sockets (live commands)

The API routes messages between cubes and the web app over WebSockets. It only routes; it does not know what the commands mean. Every frame, in both directions, is one JSON text message `{ "name", "id"?, "replyTo"?, "payload"? }` (`Models/Messages/CubeMessage.cs`).

| Endpoint | Who | Authentication |
|---|---|---|
| `GET /ws/cube?cubeId=<id>` | a cube | `Authorization: Bearer <Cubes:Secret>` |
| `GET /ws/client?cubeId=<id>&access_token=<JWT>` | the web app | the user's access token (in the query because browsers cannot set headers on a WebSocket; it is checked once, when connecting) |

- A client's message is forwarded to its cube as a **command** (the server adds an `id`). If the cube is not connected the client gets an `error` with `code: "cube_offline"` and `replyTo` the command's id; nothing is queued.
- What a cube sends is forwarded to **every client** of that cube (its `reply` carries `replyTo`).
- The server tells clients `cube_status` (`{ "online": bool }`) when they connect and whenever the cube connects or disconnects. A second connection of the same cube id replaces the first.
- `Cubes:Secret` (set `Cubes__Secret`) is the secret every cube uses; without it no cube can connect.
- `ICubeDirectory` decides who is a valid cube and who may talk to which cube. `ConfigCubeDirectory` has no database: one shared secret, and every signed-in user may use every cube. When devices are stored, implement `ICubeDirectory` over them and swap it in `Program.cs`.
- The hub (`CubeHub`) keeps its connections in memory, so it works with a single API instance.

## Profiles and follows

Every endpoint needs `Authorization: Bearer <access token>`. What a profile says (whether two users follow each other) depends on who asks, so the answers are always "as the signed-in user sees them" (`Services/ProfileService.cs`).

| Endpoint | What it does |
|---|---|
| `GET /users/me` | The signed-in user's profile, with their email. |
| `PUT /users/me` | Changes `description` (up to 280 characters) and `photo` (a `data:image/png\|jpeg\|webp\|gif;base64,...` URL of up to 400 000 characters). A blank field is cleared. |
| `GET /users?search=<text>` | Up to 20 users whose name contains the text (not the signed-in user). |
| `GET /users/{id}` | A user's profile, without their email. |
| `GET /users/{id}/followers` | Who follows the user, the most recent first. |
| `GET /users/{id}/following` | Who the user follows, the most recent first. |
| `PUT /users/{id}/follow` | The signed-in user follows them (doing it twice changes nothing). |
| `DELETE /users/{id}/follow` | The signed-in user stops following them. |

- A profile (`ProfileResponse`) has `id`, `username`, `email` (only your own), `description`, `photo`, `followers`, `following`, `followsYou`, `youFollow` and `isYou`.
- A list entry (`UserSummaryResponse`) has `id`, `username`, `description`, `photo`, `followers`, `followsYou` and `youFollow`. `followsYou && youFollow` means they follow each other.
- A follow is a row of `Follows` (`Models/Entities/Follow.cs`) keyed by the pair, so it exists once; a database check stops a user from following themselves.
- The migration `AddProfilesAndFollows` adds them; apply it with `dotnet ef database update`.

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

Expected errors are thrown as `KuvException`s (`Models/Exceptions/`), which carry an HTTP status code and a message. Any layer (services, controllers) can throw one; controllers do not catch them.

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

## Project layout

Type-only files live under `Models/`, one folder per kind: `Entities/`, `Exceptions/`, `Requests/`, `Messages/` and `Responses/`. Classes with behavior stay in `Controllers/`, `Services/` and `Data/`.
