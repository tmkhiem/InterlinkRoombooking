# Legacy-to-New Version Migration Notes

This note converts legacy findings into implementation-ready requirements for the next version.

## Functional requirements to preserve

### 1) Weekly schedule retrieval

- Input: a date (`YYYY-MM-DD`)
- Behavior: compute containing week (Monday-Sunday) and return bookings in that range
- Output shape used by UI:
  - `Id`, `Creator`, `Name`, `Title`, `Room`, `Date`, `StartTime`, `EndTime`, `State`, optional `Note`

### 2) Booking creation

- Authenticated user only
- Creator/name must come from authenticated identity (not trusted from client)
- Must reject overlapping bookings in same room/date/time range
- Room-specific policy:
  - Room `1`: starts in waiting-confirmation state
  - Rooms `2` and `3`: starts as booked

### 3) Booking confirmation

- Admin-only action
- Only valid for room `1` and waiting-confirmation state

### 4) Booking deletion

- Allowed for booking creator or admin

### 5) Session + identity

- Support interactive login flow and authenticated API access
- Include role resolution (`isAdmin`) for UI authorization behavior

## Data model baseline

Minimum entities inferred from legacy implementation:

- `Admin`
  - `Email` (primary key)
- `Booking`
  - `Id` (int)
  - `Creator` (string email)
  - `Name` (string)
  - `Title` (string)
  - `Room` (int)
  - `Date` (date)
  - `StartTime` (time)
  - `EndTime` (time)
  - `State` (int enum-like)
  - `Note` (nullable string)

## State and room conventions to formalize

- Booking states:
  - `0`: booked
  - `1`: confirmation wait
  - `2`: unused
  - `254`: confirmation rejected
  - `255`: cancelled
- Room IDs in current UI: `1`, `2`, `3`

For the new version, these should be moved to explicit enums/configurations (not magic numbers).

## API contract candidates for the new version

Recommended stable endpoints based on legacy behavior:

- `GET /api/schedules?date=`
- `POST /api/schedule`
- `PUT /api/confirm/{id}`
- `DELETE /api/schedule/{id}`
- `GET /api/self`

Also preserve login/logout routes or provide equivalent authentication endpoints for SPA compatibility.

## Non-functional and security requirements

- Remove hardcoded secrets from source-controlled config.
- Use environment-based secret stores.
- Keep strict server-side validation for overlap and authorization.
- Add structured observability (audit logs for create/confirm/delete actions).
- Add automated test coverage for booking rules.

## Known legacy gaps to avoid carrying forward

- Legacy frontend build currently fails in sandbox due existing TypeScript issues.
- Legacy code has partially disabled Telegram internals.
- Legacy app has no clearly defined domain/service layers; controller holds business logic.

## Suggested implementation priorities

1. Define formal domain model and state/room enums.
2. Rebuild booking rule engine (overlap + authorization) with unit tests.
3. Rebuild API contract and auth/session integration.
4. Rebuild schedule UI against typed API client.
5. Add migration scripts and compatibility checks for existing DB data.
