# Architecture

## Components

| Component | Responsibility |
| --- | --- |
| **Mobile app** (Expo dev build, React Native, TypeScript) | Native background location, on-device throttling, UI, LiveKit client, SignalR client. Never decides who it hears. |
| **API** (ASP.NET Core, .NET 10) | Auth, driving sessions, location ingestion & validation, proximity grouping, voice authorisation, realtime events. |
| **Redis** | Realtime presence: latest fix per driver, GEO spatial index, group rosters, liveness, SignalR backplane, proximity lease. |
| **PostgreSQL + PostGIS** | Durable data: users, logins, refresh tokens, driving sessions (and a coarse, retention-limited start cell). |
| **LiveKit** (SFU) | Audio distribution. Rooms map 1:1 to proximity groups. |

Backend layering (dependencies point inward):

```
Api ──▶ Application ──▶ Domain
 │           ▲
 └──▶ Infrastructure (implements Application interfaces: EF Core, Redis, LiveKit, JWT, workers)
```

Business logic lives in `Application` (`Features/*`); controllers and the hub are thin adapters.

## Driving session lifecycle

```
Offline ─▶ Online (signed in) ─▶ Driving (session active) ─▶ Nearby (drivers in range) ─▶ Voice (connected)
```

- `POST /driving/sessions` starts (idempotently) a session with a random per-session **handle**, which
  is the only identifier other drivers ever see and the LiveKit identity.
- Only drivers with an active session are tracked, indexed and grouped.
- Silence for `StaleAfterSeconds` (60 s) removes a driver from proximity but keeps the session alive
  (tunnels, dead zones). Silence for `IdleTimeoutMinutes` (30 min) ends the session.
- Ending a session removes every trace from Redis, closes/reconciles the voice room and pushes
  `DrivingSessionEnded` to all of the user's devices.

## Location pipeline

```
native fix (≈1–2 s, OS-driven, works when locked)
   │  on-device throttle: ≥50 m moved | ≥15 s elapsed | heading Δ≥45° at speed | speed Δ≥5 m/s
   │                      | forced: session start, app resume, network restored  (min 2.5 s apart)
   ▼
POST /api/v1/driving/location ──▶ validate ──▶ store latest fix (Redis, TTL) ──▶ enqueue evaluation
                                   │                                              │ (waits ≤2 s)
                                   └─ reject: impossible/stale/future/out-of-order/teleport
                                   └─ throttle: < 1 s since last update
                                   └─ heartbeat only: accuracy worse than 150 m
   ◀── response: fresh proximity snapshot (keeps the client in sync even without SignalR)
```

The throttle has no timers: it runs on each native callback, which the OS keeps delivering in the
background. The thresholds are served by `GET /api/v1/config` so they can be tuned without a release.

## Proximity grouping

### Candidate lookup — never O(n²)

Each evaluation looks at one driver's **neighbourhood** only:

1. `GEOSEARCH` the Redis GEO index around the driver (radius ≈ join distance + margin, nearest 64).
2. Load those candidates' sessions, and the full rosters of every group any of them is in.
3. Run the pure `ProximityGroupingEngine` on that small world; compute exact haversine distances.

Cost per update is O(k²) with k ≤ group size + candidates (tens), independent of total users.

### The grouping rules

Groups are **clique-constrained** with hysteresis:

| Rule | Condition |
| --- | --- |
| Join / form | distance ≤ `JoinDistanceMeters` (1000) to **every** member |
| Stay | every pair ≤ `LeaveDistanceMeters` (1100) |
| Leave | some pair > `LeaveDistanceMeters`; the member with the most violations leaves first (then the most peripheral), so one driver who wandered off leaves — not the majority |
| Merge | two groups merge when every cross pair ≤ join distance and the result fits `MaxGroupSize` (the smaller group moves rooms) |
| Absorb | ungrouped neighbours in range of every member are pulled in |
| Dissolve | a group of one dissolves; displaced drivers are re-evaluated immediately |
| Accuracy | fixes worse than `MaxJoinAccuracyMeters` (75 m) keep existing membership but cannot create joins |
| Liveness | members without a fresh fix are pruned |

Why cliques instead of "everyone within 1 km of someone"? Connected components chain arbitrarily far
(a traffic jam would become one 20 km voice room). Cliques guarantee every listener is within range of
every speaker, and map cleanly onto SFU rooms that the server can authorise and police. The trade-off:
a driver between two separate groups joins one of them (preferring the larger); the Nearby screen shows
the others as "In de buurt · ander kanaal".

### Consistency and scaling

- All membership writes go through a **single writer**: an in-process queue drained by
  `ProximityWorker` under a Redis lease (`opd:lock:proximity`), applying each plan atomically
  (`MULTI/EXEC`). Repeated evaluations of the same driver in a batch are coalesced.
- `IProximityCommandQueue` is the seam for moving proximity into a dedicated service: replace the
  channel with a durable stream (Redis Streams / Kafka) partitioned by region (e.g. geohash-5 cells
  with neighbour-cell locking), and run one writer per partition.
- Redis keys: `session:{user}`, `pos:{user}`, `geo`, `seen`, `grp:{group}` (see `RedisKeys`). For a
  Redis Cluster, the GEO index would be sharded per region alongside the partitioning above.

## Realtime (SignalR)

Strongly typed hub `IProximityClient` at `/hubs/proximity` (JWT via `access_token` for WebSockets,
Redis backplane for multiple API instances):

| Event | When |
| --- | --- |
| `ProximityGroupJoined(group)` | you were placed in a group (includes a voice token for that room) |
| `NearbyUsersChanged(group)` | your group's roster changed |
| `ProximityGroupLeft(left)` | you no longer belong to any group (reason included) |
| `DrivingSessionStarted(session)` / `DrivingSessionEnded(ended)` | session state changed (any device, idle timeout) |

Hub methods: `GetSnapshot()` (resync after reconnect, includes a voice token) and
`RequestVoiceToken()`. Speaking state and voice-room participant join/leave come from LiveKit
itself and are not duplicated. Coordinates are never sent to other clients.

## Voice

```
group joined ─▶ API issues token (identity = handle, room = opd-{group}, audio-only, 5 min)
            ─▶ client connects to LiveKit (publishes mic once; mute = track mute, no re-acquire)
group left / session ended ─▶ client disconnects
                           ─▶ API reconciles the room: lists participants, removes anyone not
                              in the authoritative roster, deletes rooms of dissolved groups
LiveKit participant_joined webhook (signed) ─▶ reconcile again (defeats replayed tokens)
```

Reconciliation runs in `VoiceRoomReconciler`, off the proximity hot path, so a slow SFU never delays
grouping.

## Failure handling

| Failure | Behaviour |
| --- | --- |
| No network / tunnel | Client backs off (2–30 s), keeps the session; server keeps the session for 30 min; proximity drops after 60 s and resumes on the next fix. |
| SignalR drop | Automatic reconnect forever (capped backoff) + snapshot resync on reconnect; location responses also carry snapshots. |
| LiveKit drop | LiveKit resumes transparently; on hard failure the client refetches a token and rejoins with backoff. |
| Server evicted the client | Client follows the next proximity event/snapshot. |
| Redis lost | Location ingestion answers `409 no_active_session`; the client idempotently restarts its session. |
| GPS weak / unavailable | Low-accuracy fixes are heartbeats only; UI shows "Weak GPS" / "Waiting for GPS". |
| Mic denied/unavailable | Listen-only fallback with a banner. |
| Server ended session | `DrivingSessionEnded` stops tracking/voice on every device. |
