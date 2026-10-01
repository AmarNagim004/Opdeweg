# Configuration

The API uses standard ASP.NET Core configuration (`appsettings.json` → `appsettings.{Environment}.json`
→ user secrets → environment variables). The documented flat variables below are mapped onto
configuration keys, and override them.

## Environment variables (backend)

| Variable | Maps to | Notes |
| --- | --- | --- |
| `DATABASE_CONNECTION_STRING` | `ConnectionStrings:Database` | Npgsql connection string. PostGIS extension required. |
| `REDIS_CONNECTION_STRING` | `ConnectionStrings:Redis` | StackExchange.Redis string, e.g. `redis:6379,password=...,ssl=true`. |
| `JWT_SECRET` | `Jwt:Secret` | ≥ 32 bytes; the API refuses to start otherwise. |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `Jwt:Issuer` / `Jwt:Audience` | Defaults `opdeweg-api` / `opdeweg-mobile`. |
| `LIVEKIT_URL` | `LiveKit:Url` | URL **clients** connect to (`wss://` in production). |
| `LIVEKIT_API_URL` | `LiveKit:ApiUrl` | Optional URL the API uses for RoomService (defaults to `LIVEKIT_URL` as http/https). |
| `LIVEKIT_API_KEY` | `LiveKit:ApiKey` | |
| `LIVEKIT_API_SECRET` | `LiveKit:ApiSecret` | Server-only. Never shipped to the app. |

Any other key can be set with the `Section__Key` convention, e.g. `Proximity__JoinDistanceMeters=800`.

## Options

### `Proximity`

| Key | Default | Meaning |
| --- | --- | --- |
| `JoinDistanceMeters` | 1000 | Join/form when within this distance of every member. |
| `LeaveDistanceMeters` | 1100 | Leave when beyond this distance of any member. Must be > join (validated). |
| `MaxGroupSize` | 12 | Cap per voice group. |
| `CandidateSearchLimit` | 64 | Nearest candidates fetched from the spatial index per evaluation. |
| `StaleAfterSeconds` | 60 | No update for this long → removed from proximity (session kept). |
| `SweepIntervalSeconds` | 10 | Stale/idle sweep cadence. |
| `MaxUsableAccuracyMeters` | 150 | Worse fixes only refresh liveness. |
| `MaxJoinAccuracyMeters` | 75 | Worse fixes can't create joins (existing groups are kept). |
| `DistanceRoundingMeters` | 50 | Granularity of distances shown to other drivers. |
| `NearbyListLimit` | 25 | Max "also nearby" drivers in a snapshot. |

### `Location`

| Key | Default | Meaning |
| --- | --- | --- |
| `ClientDistanceThresholdMeters` | 50 | App transmits after moving this far. |
| `ClientMaxIntervalSeconds` | 15 | App transmits at least this often. |
| `ClientMinIntervalMilliseconds` | 2500 | App never transmits faster (unless forced). |
| `ClientHeadingChangeDegrees` | 45 | Heading change trigger (only at speed). |
| `ClientSpeedChangeMetersPerSecond` | 5 | Speed change trigger. |
| `MinUpdateIntervalMilliseconds` | 1000 | Server throttle per user. |
| `MaxTimestampAgeSeconds` | 60 | Older client timestamps are rejected. |
| `MaxTimestampSkewSeconds` | 30 | Future skew tolerated. |
| `MaxPlausibleSpeedMetersPerSecond` | 90 | Implied speed above this = implausible movement (422). |
| `MaxReportedAccuracyMeters` | 10000 | Sanity bound. |
| `MovementToleranceMeters` | 50 | Slack for GPS/network jitter in the plausibility check. |
| `EvaluationTimeoutMilliseconds` | 2000 | How long a location request waits for its proximity evaluation. |

The `Client*` values are served to the app via `GET /api/v1/config`.

### `DrivingSession`

| Key | Default | Meaning |
| --- | --- | --- |
| `IdleTimeoutMinutes` | 30 | Session ends after this long without updates. |
| `StoreCoarseStartArea` | true | Store a grid-snapped start cell (PostGIS) for capacity planning. |
| `CoarseAreaGridDegrees` | 0.05 | Grid size (~5 km). |
| `CoarseAreaRetentionDays` | 30 | Purged afterwards (hourly job). |

### Others

| Section | Keys |
| --- | --- |
| `Auth` | `RefreshTokenLifetimeDays` (30), `MinPasswordLength` (8) |
| `Jwt` | `AccessTokenLifetimeMinutes` (15) |
| `LiveKit` | `TokenTtlSeconds` (300) |
| `Redis` | `KeyPrefix` (`opd:`), `UseSignalRBackplane` (true) |
| `RateLimiting` | `AuthPermitsPerMinute` (10/IP), `GlobalPermitsPerMinute` (240/user), `LocationBurst` (20), `HubInvocationsPerTenSeconds` (30) |
| `Database` | `MigrateOnStartup` (true in Development, false otherwise) |

Production logging is JSON (`Logging:Console:FormatterName=json`); Development uses single-line text.

## Mobile app

| Variable | Notes |
| --- | --- |
| `EXPO_PUBLIC_API_URL` | Backend base URL. Bundled into the app — **never** put secrets in `EXPO_PUBLIC_*`. Release builds refuse non-`https://` URLs. |
| `APP_VARIANT` | `development` builds "Opdeweg Dev" (separate bundle id) and allows cleartext HTTP. |

Runtime tuning (thresholds) comes from the server; user preferences (theme, start muted, keep screen
on) are stored locally.
