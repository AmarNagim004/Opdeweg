# Development

## Prerequisites

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0 (see `global.json`) |
| Node.js | 20+ (22 recommended) |
| Docker | with Compose v2 |
| iOS | macOS + Xcode 16+ and CocoaPods (for `expo run:ios`) |
| Android | Android Studio + SDK 35/36, a device or emulator with Google Play services |

## 1. Configure

```bash
cp .env.example .env
# Generate secrets (32+ bytes):
openssl rand -base64 48   # → JWT_SECRET
openssl rand -base64 48   # → LIVEKIT_API_SECRET
```

Set `POSTGRES_PASSWORD` and use the same password in `DATABASE_CONNECTION_STRING`.
`.env` is git-ignored; never commit it. The API loads it automatically in `Development`.

## 2. Start infrastructure

```bash
docker compose up -d        # postgis/postgis:16-3.4, redis:7.4, livekit-server:v1.13.7
docker compose ps
```

LiveKit posts webhooks to `LIVEKIT_WEBHOOK_URL` (default: the API on your host via
`host.docker.internal`).

## 3. Run the API

```bash
dotnet run --project src/Api          # http://0.0.0.0:5080, migrations applied on start in Development
curl http://localhost:5080/health/ready
curl http://localhost:5080/api/v1/config
```

OpenAPI (Development only): `http://localhost:5080/openapi/v1.json`.

Alternatively run everything in containers: `docker compose --profile full up -d --build`.

### Database migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Infrastructure
```

The design-time factory reads `DATABASE_CONNECTION_STRING`. In production set
`Database:MigrateOnStartup=false` and apply migrations from your deploy pipeline
(`dotnet ef migrations bundle`).

## 4. Run the mobile app

Opdeweg uses native modules (LiveKit/WebRTC, background location, a local foreground-service
module), so it runs as an **Expo development build** — not in Expo Go.

```bash
cd src/Mobile
npm install
cp .env.example .env.local
```

Edit `.env.local`:

- `EXPO_PUBLIC_API_URL` — `http://<your computer's LAN IP>:5080` for a physical phone
  (`http://10.0.2.2:5080` for the Android emulator, `http://localhost:5080` for the iOS simulator).
- Set `LIVEKIT_URL` in the **backend** `.env` to `ws://<LAN IP>:7880` and `LIVEKIT_NODE_IP` to the
  same LAN IP so the phone can reach the SFU's media ports (UDP 50000–50100, TCP 7881).

```bash
APP_VARIANT=development npx expo run:ios       # or run:android; builds & installs "Opdeweg Dev"
npx expo start --dev-client                    # subsequent JS-only changes
```

Cloud builds: `npx eas-cli build --profile development` (configure `eas.json` for your account).

### Testing proximity without driving

- Two simulators/emulators (or one device + one simulator) with different accounts.
- iOS Simulator: *Features → Location → Custom Location / Freeway Drive*.
- Android emulator: *Extended controls → Location* (routes / GPX playback).
- The server rejects implausible jumps (> 90 m/s): move gradually, or wait a few seconds between big jumps.
- Release builds refuse mock-provider locations; development builds allow them.

## 5. Tests and checks

```bash
# Backend
dotnet build Opdeweg.slnx                                   # analyzers run as errors
dotnet test --project src/Tests/Opdeweg.UnitTests
dotnet test --project src/Tests/Opdeweg.IntegrationTests    # needs PostgreSQL (PostGIS) + Redis

# Mobile
cd src/Mobile
npm run typecheck && npm run lint && npm test
npx expo prebuild --no-install --clean                      # validate native config (git-ignored output)
```

Integration tests use `OPDEWEG_TEST_DATABASE` (default
`Host=localhost;Port=5432;Database=opdeweg_test;Username=opdeweg;Password=opdeweg`) and
`OPDEWEG_TEST_REDIS` (default `localhost:6379,defaultDatabase=15`). The test database's `public`
schema is **dropped and recreated** on every run — point it at a dedicated database. Tests are
skipped (not failed) when either service is unreachable.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Phone can't reach the API | Use your LAN IP, same Wi-Fi, firewall allows 5080; dev builds allow HTTP only with `APP_VARIANT=development`. |
| Voice never connects on a device | `LIVEKIT_URL`/`LIVEKIT_NODE_IP` must be reachable from the phone; open UDP 50000–50100 and TCP 7881. |
| `Jwt:Secret ... at least 32 bytes` on startup | Set a long `JWT_SECRET`. |
| No location updates on Android when locked | Ensure the "Driving" notification is visible; disable battery optimisation for the app on aggressive OEMs. |
| `no_active_session` (409) | The session expired server-side; the app restarts it automatically if you're still driving. |
