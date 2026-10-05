# API

Base path `/api/v1`. JSON uses camelCase; enums are camelCase strings. Errors are RFC 7807
problem details with a stable `code` (e.g. `invalid_credentials`, `no_active_session`,
`implausible_movement`, `not_in_group`, `rate_limited`, `validation_failed`).
All endpoints require `Authorization: Bearer <access token>` unless marked public.

## Auth

| Method & path | Body | Response |
| --- | --- | --- |
| `POST /auth/register` (public) | `{ email, password, displayName }` | `AuthResponse` |
| `POST /auth/login` (public) | `{ email, password }` | `AuthResponse` |
| `POST /auth/refresh` (public) | `{ refreshToken }` | `AuthResponse` (rotated) |
| `POST /auth/logout` | `{ refreshToken? }` | 204 — revokes the token family and ends any drive |

`AuthResponse = { accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user: Me }`

## Profile

| Method & path | Notes |
| --- | --- |
| `GET /me` | `Me = { id, email, displayName, avatarUrl, shareDisplayName, createdAt }` |
| `PATCH /me` | `{ displayName?, avatarUrl? (https), shareDisplayName? }` |
| `DELETE /me` | Deletes the account and all data. |

## Driving

| Method & path | Notes |
| --- | --- |
| `GET /driving/sessions/current` | 200 `DrivingSession` or 204 |
| `POST /driving/sessions` | Start (idempotent). `DrivingSession = { id, handle, startedAt, endedAt, endReason }` |
| `POST /driving/sessions/current/end` | 204 |
| `POST /driving/location` | `{ latitude, longitude, speed?, heading?, accuracy?, timestamp }` → `{ status: accepted \| lowAccuracy \| throttled, proximity: ProximitySnapshot? }`. 400 invalid, 409 `no_active_session`, 422 `implausible_movement` / `out_of_order`, 429 rate limited. |

## Proximity & voice

| Method & path | Notes |
| --- | --- |
| `GET /proximity?includeVoice=false` | `ProximitySnapshot` |
| `POST /voice/token` | `VoiceAccess` for your current group, or 409 `not_in_group` |
| `GET /config` (public) | Client tuning: join/leave distances, location throttle thresholds |
| `POST /livekit/webhook` (LiveKit-signed) | Participant joins trigger room reconciliation |

```ts
ProximitySnapshot = { sessionActive, group: ProximityGroup | null, nearby: NearbyDriver[], nearbyCount, generatedAt }
ProximityGroup    = { groupId, members: NearbyDriver[], voice: VoiceAccess | null }
NearbyDriver      = { id /* per-session handle */, displayName, avatarUrl, approxDistanceMeters, inVoiceGroup }
VoiceAccess       = { url, token, room, identity, expiresAt }
```

## SignalR hub — `/hubs/proximity`

Authenticate with `?access_token=` (WebSockets) or the `Authorization` header.

| Server → client | Payload |
| --- | --- |
| `ProximityGroupJoined` | `ProximityGroup` (with `voice`) |
| `NearbyUsersChanged` | `ProximityGroup` (roster update, no token) |
| `ProximityGroupLeft` | `{ groupId, reason: proximity \| outOfRange \| merged \| groupDissolved \| sessionEnded \| stale }` |
| `DrivingSessionStarted` | `DrivingSession` |
| `DrivingSessionEnded` | `{ sessionId, reason, endedAt }` |

| Client → server | Returns |
| --- | --- |
| `GetSnapshot()` | `ProximitySnapshot` (includes a voice token when grouped) |
| `RequestVoiceToken()` | `VoiceAccess` or `null` |

## Health

`GET /health/live` (process up) and `GET /health/ready` (PostgreSQL + Redis), both public.
