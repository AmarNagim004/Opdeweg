# Privacy & security

Location is treated as sensitive personal data throughout.

## Data inventory

| Data | Where | Retention |
| --- | --- | --- |
| E-mail, display name, avatar URL, privacy preference | PostgreSQL `users` | Until account deletion (`DELETE /api/v1/me` removes everything). |
| Password hash (ASP.NET Core Identity PBKDF2) | PostgreSQL `user_logins` | Until deletion. |
| Refresh tokens (SHA-256 hashes only) | PostgreSQL `refresh_tokens` | Pruned 7 days after expiry/revocation. |
| Driving sessions (start/end, reason, random handle) | PostgreSQL `driving_sessions` | Until account deletion. |
| Coarse start cell (~5 km grid) | PostgreSQL / PostGIS | Purged after 30 days; can be disabled (`DrivingSession:StoreCoarseStartArea=false`). |
| **Latest** position fix | Redis `pos:{user}` | Overwritten on every update; expires ≈3 min after the last update; deleted when the drive ends. |
| Spatial index entry | Redis `geo` | Removed after 60 s of silence or when the drive ends. |
| Group membership | Redis | Only while driving. |

No location history or route trail is stored anywhere. Redis runs without persistence in development.

## What other drivers learn

- A **per-session pseudonym** (`d1a2b…`) — not your account ID; it changes every drive.
- Your display name, or "Driver" if you turned *Show my name* off (avatar hidden too).
- A distance **rounded to 50 m**, computed server-side. Never coordinates, heading or speed.
- Only drivers who are themselves nearby and driving receive anything.

## Logging

Structured logs contain user/session/group IDs, reasons and implied speeds — never coordinates,
tokens or request bodies. `GeoPoint.ToString()` is redacted so coordinates cannot leak into logs by
accident, and the access log omits query strings (the hub token travels there).

## Security model

| Threat | Mitigation |
| --- | --- |
| Credential stuffing | Per-IP rate limit on auth endpoints, PBKDF2 hashing, uniform timing for unknown accounts. |
| Token theft | 15-min JWTs; rotating refresh tokens with reuse detection (family revoked); tokens in Keychain/Keystore (`AFTER_FIRST_UNLOCK`). |
| Eavesdropping on far-away drivers | Server decides membership; LiveKit tokens are single-room, 5-minute, audio-only; rooms are reconciled against rosters on every change **and** on every LiveKit join webhook. |
| Location spoofing | Coordinate/timestamp/order validation, implied-speed check (> 90 m/s rejected), per-user throttling, mock locations refused in release builds. (A determined spoofer with consistent fakes can still pose as nearby — see below.) |
| Abuse / DoS | Global per-user rate limits, location token bucket, hub invocation limits, bounded queues. |
| Secret leakage | No secrets in the repo or the app; `.env` git-ignored; LiveKit secret only on the server; HTTPS enforced in release builds and HSTS/HTTPS redirection on the API. |
| Trilateration of a driver | Distances are rounded and only visible to drivers within ~1 km (who could see the car anyway). |

Residual risk: a client can fabricate a plausible trajectory. Hardening options are device integrity
attestation (App Attest / Play Integrity) and server-side anomaly detection.
