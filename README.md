# Opdeweg

**Talk to the road.** Opdeweg is a cross-platform (iOS + Android) proximity voice app for car and
motorcycle drivers. Start a drive and you are automatically placed in a voice channel with drivers
within ~1 km; drive apart and you leave it again. No channels to browse, nothing to tap while moving.

```
React Native (Expo dev build) ──WebRTC──▶ LiveKit SFU
        │  REST + SignalR                    ▲  short-lived, single-room tokens
        ▼                                    │  room admin + signed webhooks
  ASP.NET Core (.NET 10) ────────────────────┘
        │
        ├── Redis       presence · GEO spatial index · group rosters (ephemeral, TTL'd)
        └── PostgreSQL  users · logins · driving sessions (+ PostGIS coarse start cell)
```

## Highlights

- **Server-authoritative proximity.** Clients send throttled fixes; the backend finds candidates via a
  Redis GEO index (no O(n²) comparisons), checks exact distances and decides group membership.
- **Groups that never chain.** Voice groups are clique-constrained: everyone is within the join distance
  of everyone else, so `Alice –600 m– Bob –700 m– Charlie` never puts Alice and Charlie (1.3 km) together.
- **Hysteresis.** Join at ≤ 1000 m, leave at > 1100 m (configurable) — no flapping from GPS noise.
- **Voice via LiveKit.** One room per proximity group; the API issues 5-minute, audio-only tokens for
  exactly that room and evicts anyone the roster no longer allows (also on every LiveKit join webhook).
- **Built for locked phones.** Native background location (no JS timers), an Android foreground service
  typed `location|microphone`, iOS `location` + `audio` background modes, Bluetooth/car/intercom routing.
- **Private by design.** Only the latest fix is kept (in Redis, with TTLs); other drivers see a per-session
  pseudonym, a display name (or "Driver") and a distance rounded to 50 m — never coordinates.
- **"Night Road" theme.** Asphalt neutrals with a signal-lime accent, dark and light modes, large
  glanceable controls.

## Repository layout

```
├── docker-compose.yml        PostgreSQL/PostGIS, Redis, LiveKit (+ API with --profile full)
├── .env.example              Backend configuration template (no secrets committed)
├── Opdeweg.slnx              .NET solution
├── docs/                     Architecture, development, configuration, permissions, privacy, API
└── src/
    ├── Api/                  ASP.NET Core host: controllers, SignalR hub, middleware
    ├── Application/          Use cases, proximity engine, DTOs, interfaces (no infrastructure deps)
    ├── Domain/               Entities, value objects (GeoPoint, GeoMath), enums
    ├── Infrastructure/       EF Core + PostGIS, Redis presence, LiveKit, JWT, background workers
    ├── Tests/                xUnit unit + integration tests
    └── Mobile/               Expo / React Native app (TypeScript, React Navigation, Zustand)
```

## Quick start

```bash
cp .env.example .env            # fill in JWT_SECRET, LIVEKIT_API_SECRET, POSTGRES_PASSWORD
docker compose up -d            # PostGIS, Redis, LiveKit
dotnet run --project src/Api    # http://localhost:5080 (applies migrations in Development)

cd src/Mobile
npm install
cp .env.example .env.local      # EXPO_PUBLIC_API_URL=http://<your-LAN-IP>:5080
npx expo run:ios                # or: npx expo run:android  (a development build — not Expo Go)
```

Full instructions: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Tests

```bash
dotnet test --project src/Tests/Opdeweg.UnitTests          # proximity engine, validation, voice auth, LiveKit
dotnet test --project src/Tests/Opdeweg.IntegrationTests   # real PostgreSQL + Redis (skips if unavailable)
cd src/Mobile && npm test && npm run typecheck && npm run lint
```

## Documentation

| Document | Contents |
| --- | --- |
| [Architecture](docs/ARCHITECTURE.md) | Components, proximity algorithm, realtime flow, scaling path |
| [Development](docs/DEVELOPMENT.md) | Running everything locally, device testing, troubleshooting |
| [Configuration](docs/CONFIGURATION.md) | Environment variables and every tunable option |
| [Permissions & background](docs/PERMISSIONS.md) | iOS/Android permissions, background audio/location, Bluetooth |
| [Privacy & security](docs/PRIVACY.md) | Data inventory, retention, what other drivers can see, threat model |
| [API](docs/API.md) | REST endpoints and the SignalR hub contract |
