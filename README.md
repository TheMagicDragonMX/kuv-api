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
| `POST /session/register` | Creates an account with a username, email, age (`0`-`150`) and password. A `409` says which of the email or the username is taken. |
| `POST /session/login` | `{ username, password }`. The `username` field accepts the username **or the email**, ignoring case. A Google-only account answers `401` with a message saying to use Google. |
| `POST /session/refresh` | Exchanges a refresh token for a new pair (each refresh token works once). |
| `POST /session/google` | `{ idToken }` with the ID token Google gave the browser. Answers `{ status: "signedIn", tokens }`, or `{ status: "needsProfile", signupToken, email }` when no user has that Google account yet. |
| `POST /session/google/complete` | `{ signupToken, age, username }`. Creates the account and answers the tokens. |
| `GET /session/username-available?username=` | `{ available }`, ignoring case. `400` when it is not 3 to 50 characters. |
| `GET /session/email-available?email=` | `{ available }`, ignoring case. Accounts made with Google count. |

How `POST /session/google` decides (`Services/GoogleAuthService.cs`):

1. The ID token is checked with Google (signature, issuer, expiry, and that its audience is `Google:ClientId`). Google must also have verified the email.
2. A user with that Google id (`Users.GoogleId`) signs in.
3. Otherwise a user with the same email (ignoring case) **is linked**: their `GoogleId` is saved and they sign in. If that user is already linked to a different Google account the answer is `409`.
4. Otherwise nothing is created yet. The API answers `needsProfile` with a `signupToken`: a short-lived JWT (15 minutes) signed by the API that remembers the verified Google id and email. Its audience differs from the access tokens', so neither can be used as the other. The client asks for the age (`0`-`150`) and a unique username (`3`-`50` characters, compared ignoring case), then calls `/session/google/complete`.

The web app asks for the age and the username in the same two steps whichever way the user signs up: with a password it first takes the email and password (checking `email-available`), then the two steps, then calls `register`; with Google it calls `google`, then the two steps, then `google/complete`. Username and age rules live in `UserService` and are shared by both.

Accounts created with Google have no password (`Users.Hashword` is null) and can only sign in with Google. Linked accounts keep their password.

Configuration: set `Google:ClientId` (environment variable `Google__ClientId`) to the OAuth client ID of the Google Cloud project; for local development put `Google__ClientId=...` in `.env`. Deployment fills it from the `GOOGLE_CLIENT_ID` repository variable (`Deploy/kuv-api.env`). Without it, `/session/google` answers `500` and password login keeps working. The same client ID goes in the web app's `environments/*.ts`. No client secret is needed: the API only verifies tokens, it never talks to Google on the user's behalf.

The migration `AddTimestamps` adds `Users.CreatedAt` and `Users.UpdatedAt` (existing users get the migration time); apply it with `dotnet ef database update`.

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

Every endpoint needs `Authorization: Bearer <access token>`, except `GET /users/{id}/photo`. What a profile says (whether two users follow each other) depends on who asks, so the answers are always "as the signed-in user sees them" (`Services/ProfileService.cs`).

| Endpoint | What it does |
|---|---|
| `GET /users/me` | The signed-in user's profile, with their email. |
| `PUT /users/me` | Changes `bio` (up to 280 characters). A blank bio is cleared. |
| `PUT /users/me/photo` | Sets the profile picture: a multipart form with a `photo` file (PNG, JPEG, WebP or GIF, up to 300 KB). Answers the profile. |
| `DELETE /users/me/photo` | Removes the profile picture (doing it with none changes nothing). Answers the profile. |
| `GET /users/{id}/photo` | The picture itself. **No token needed**, so a plain `<img>` can load it. `404` when the user has none. |
| `GET /users?search=<text>` | Up to 20 users whose name contains the text (not the signed-in user). |
| `GET /users/{id}` | A user's profile, without their email. |
| `GET /users/{id}/followers` | Who follows the user, the most recent first. |
| `GET /users/{id}/following` | Who the user follows, the most recent first. |
| `PUT /users/{id}/follow` | The signed-in user follows them (doing it twice changes nothing). |
| `DELETE /users/{id}/follow` | The signed-in user stops following them. |

- A profile (`ProfileResponse`) has `id`, `username`, `email` (only your own), `bio`, `photo`, `followers`, `following`, `followsYou`, `youFollow` and `isYou`.
- A list entry (`UserSummaryResponse`) has `id`, `username`, `bio`, `photo`, `followers`, `followsYou` and `youFollow`. `followsYou && youFollow` means they follow each other.
- `photo` is `null` when the user has no picture, otherwise a **path on this API** such as `/users/7/photo?v=639304580747926611`: put the API address in front of it. `v` is the moment the picture was saved, so the address changes whenever the picture does.
- A follow is a row of `Follows` (`Models/Entities/Follow.cs`) keyed by the pair, so it exists once; a database check stops a user from following themselves.
- The migration `AddProfilesAndFollows` adds them; apply it with `dotnet ef database update`.

### Profile pictures

Pictures are not in the database. Each is a file in the file store, in `pfp/`, named after the user's id and the type of the image (`7.png`, `7.jpg`, `7.webp` or `7.gif`); a user has at most one, and a user has a picture when its file exists (`Services/ProfilePhotoService.cs`).

- The type is read from the first bytes of the file, never from its name or the `Content-Type` the client sends, so a file that is not really an image is refused (`400`) and the served `Content-Type` is always right. Pictures are served with `X-Content-Type-Options: nosniff`.
- `GET /users/{id}/photo` answers `Cache-Control: public, max-age=31536000, immutable` when the address has `?v=` (the ones the profiles give) and supports `If-Modified-Since`.
- The pictures are public to anyone who knows a user id, which is no more than any registered account can already see (registration is open).
- See [File storage](#file-storage) for where `pfp/` and `creations/` physically live. Back that folder up with the database.

## File storage

Programs, videos and profile pictures are files, not database rows, and they go through one `IFileStore` (`Services/Files/`). Since the database is shared by every API that connects to it, the files must be too, so there are two stores:

- **`Remote`** (default): the files live on the VPS. The API reads and writes them over HTTP at `Files:RemoteUrl` (`https://files.kuv.lol/`), which is a WebDAV folder shared by nginx on its own host, so an API on a laptop sees the same files as the one on the server. It needs `Files:RemoteKey`.
- **`Disk`**: the files live in a folder of this machine, `Files:Root` (default `uploads/` next to the binaries). The server uses it, and a laptop can use it to work with no connection.

| Setting | Meaning |
| --- | --- |
| `Files__Mode` | `Remote` (default) or `Disk` |
| `Files__RemoteUrl` | Address of the shared folder, default `https://files.kuv.lol/` |
| `Files__RemoteKey` | Password of the shared folder. Put it in your local `.env` (git-ignored), never in a committed file |
| `Files__Root` | Folder used in `Disk` mode |

- **Working offline:** set `Files__Mode=Disk` in `.env` and restart. Files saved in that mode stay on your machine; the server will not find them, and they are not uploaded when you go back to `Remote`.
- **On the server** the API runs with `Files__Mode=Disk` and `Files__Root=/var/www/kuv-files`. They are in `Deploy/kuv-api.env`, the template every deploy turns into the server's `kuv-api.env` (overwriting it, so never set them by hand there), and nginx serves that same folder at `https://files.kuv.lol/` with HTTP basic auth (user `kuv`, password = the key). If `Files__Mode` were missing there, the API would try to call itself, so it refuses to start without `Files__RemoteKey`.
- **nginx** (`/etc/nginx/conf.d/kuv-client.conf`, the `files.kuv.lol` block, with a proxied DNS record in Cloudflare): `root /var/www/kuv-files` and a `location /` with `dav_methods PUT DELETE`, `create_full_put_path on`, `auth_basic` against `/etc/nginx/kuv-files.htpasswd`, `client_max_body_size 110m`. The folder `/var/www/kuv-files` is owned by `root:kuv-files` with the setgid bit; `nginx` is in that group and the API service runs with `UMask=0002` (`/etc/systemd/system/kuv-api.service.d/files.conf`), so both can replace and delete each other's files. SELinux label: `httpd_sys_rw_content_t`.
- **Debug view** (`https://peek.kuv.lol/`): the same folder, read only (`GET`/`HEAD`, anything else is `403`), with folder listing and **no password**, to look at what is stored straight from the browser. It shows **private creations and their programs too**, so anyone who knows the address can read everything. It has its own proxied DNS record in Cloudflare. If that ever matters, put a login in front of it (Cloudflare Access, or `auth_basic` in its nginx block).
- To rotate the key: `openssl rand -hex 32`, write `kuv:$(openssl passwd -apr1 KEY)` to `/etc/nginx/kuv-files.htpasswd`, reload nginx, and update every `.env`.
- The folder is **data, not part of the deploy** (it is outside `/var/www/kuv-api`). Back it up with the database.

## Creations

A creation is split in two. **The database describes it** (`Models/Entities/Creation.cs`): owner, title, description, visibility, whether it has a video, and when it was created and updated. **Its folder holds the program**: `creations/{id}/` in the file store has `program.json` and the preview video, recorded twice by the web app: over the dark background (`preview-dark.webm` or `.mp4`) and over the light one (`preview-light.webm` or `.mp4`), so each theme shows the cube on its own background. Each creation lives in its own folder and deleting it deletes the folder (`Services/CreationStorage.cs`).

```
creations/7/
├── program.json        the complete program (see below)
├── preview-dark.webm   the recording of the last save, over the dark background
└── preview-light.webm  the same, over the light background
```

### The program file

`program.json` is the complete program of the cube: the program it runs and the environment (stage) it runs on. Its format is defined by [`Schemas/program-file.schema.json`](Schemas/program-file.schema.json) (JSON Schema 2020-12, embedded in the API), and `Services/ProgramFileValidator.cs` checks every program file against it before anything is stored; a document that does not follow it is refused with `400` and a message saying where (`/program/scripts/0/trigger/seconds: 0 should be greater than 0`).

```json
{
  "version": 1,
  "program": { "version": 1, "objects": [], "scripts": [ { "trigger": { "type": "start" }, "blocks": [] } ] },
  "environment": { "background": { "FRONT": ["#000000", "..."] }, "brightness": { "objects": 1 } }
}
```

- The schema fixes the structure: `version`, `program` (`version`, `config`, `objects` as `create` blocks, `scripts` with a trigger and blocks that have an `op`) and `environment` (`background`, `backgrounds`, `startBackground`, `brightness`). Pictures are 64 × 64 colors written `#RRGGBB`, like the ones in the blocks.
- The fields of each block are **not** in the schema: they are checked by the engine (`kuv-client/src/app/cube/engine/blocks`), so they live in one place only. When the structure changes, change the schema, `program.ts` in the client and the Python port together, and bump `version`.

### Feed

The home page of the web app is a feed of categories. Each category has its own endpoint under `/feed` (`Controllers/FeedController.cs`, `Services/FeedService.cs`) and needs a token. They only ever return **public** creations, with the author's username and picture, and take `?limit=` (default `20`, between `1` and `50`).

| Endpoint | What it does |
|---|---|
| `GET /feed/new` | The newest public creations, the most recent first. Each one has `id`, `title`, `authorId`, `authorUsername`, `authorPhoto` (a path on the API, or null), `hasVideo` and `createdAt`; its video is at `/creations/{id}/video?theme=dark` (or `light`). |

The other categories (most liked today, this week, all time) do not have an endpoint yet: the web app shows them empty.

### Endpoints

- Lists (`GET /creations/me`, `GET /creations/user/{id}`) return the description of each creation, **without** its program. `GET /creations/{id}`, `POST /creations` and `PUT /creations/{id}` return the description plus `programFile`, the content of `program.json` as an object.
- `POST` and `PUT` are `multipart/form-data` with `title`, `description`, `isPublic`, `programFile` (the program file as JSON text) and optionally `videoDark` and `videoLight`, the two recordings, which come together or not at all (`400` when only one does). In a `PUT`, a missing `programFile` or missing videos leave the stored ones as they were.
- `GET /creations/{id}/video?theme=` serves the recording for `dark` (the default) or `light`; any other theme is `400`. Creations saved when there was a single `preview.webm` serve that one for both themes until their video is generated again.
- The migration `DropCreationPrograms` removed the old `Creations.ProgramJson` and `EnvironmentJson` columns, **deleting the creations saved before programs moved to files** (there was no program file to keep them in). Their loose videos in `creations/*.webm`, if any are left on a server, can be deleted by hand.

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
