# Legacy Room Booking System Overview

This document summarizes the legacy implementation under `/legacy` to support rebuilding a new version.

## Scope

- Frontend: `/legacy/frontend` (Vite + React + TypeScript + Ant Design)
- Backend: `/legacy/backend` (ASP.NET Core 8 Web API + EF Core SQL Server)

## High-level architecture

- The frontend is a single-page app that renders a weekly schedule and calls backend APIs under `/api/*`.
- The backend is cookie-authenticated and supports Microsoft and Yandex login flows.
- Backend uses EF Core with SQL Server for booking/admin data.
- Booking create/confirm/delete operations trigger Telegram notification calls (currently mostly disabled in debug/non-debug blocks).

## Frontend behavior and structure

### Main flow

- Entry point: `src/main.tsx`
- Main app: `src/App.tsx`
- On app load (and week change), frontend:
  1. Calls `GET /api/schedules?date=YYYY-MM-DD`
  2. Calls `GET /api/self`
  3. Redirects to `/api/signin` when `GET /api/self` fails

### Core UI modules

- `App.tsx`
  - Week picker and previous/next week navigation
  - Booking creation modal (`ModalBooking`)
  - Weekly grid (`WeekHorizontal`)
  - Account dropdown + signout link
- `components/WeekHorizontal.tsx`
  - Renders 7-day rows and hourly columns (07:00-20:00)
  - Draws bookings as absolute-positioned blocks in row lanes by room
  - Opens booking details modal on booking click
- `components/ModalBooking.tsx`
  - Creates new booking payload and submits via `POST /api/schedule`
- `components/ModalBookingDetails.tsx`
  - Deletes booking via `DELETE /api/schedule/{id}`
  - Admin confirmation via `PUT /api/confirm/{id}`
- `Schedule.tsx`
  - Shared TS types for schedule/week schedule

### Frontend domain assumptions

- Room IDs used in UI: `1`, `2`, `3`
- Booking state values used in UI/CSS:
  - `0`: booked
  - `1`: waiting confirmation
  - `2`: unused
  - `254`: confirmation rejected
  - `255`: cancelled
- API responses are expected in **PascalCase** fields (`Id`, `Creator`, `StartTime`, etc.)

## Backend behavior and structure

### App startup

- File: `legacy/backend/Program.cs`
- Configures:
  - Cookie authentication as default session scheme
  - External auth providers: Microsoft (`AzureAD`) and Yandex
  - Controllers with JSON naming policy disabled (keeps PascalCase)
  - EF Core `RoombookingContext`
  - Forwarded headers and trusted internal network
  - Hosted `TelegramBotService`

### Data access

- `Models/RoombookingContext.cs`
  - `Admins` table: admin users keyed by email
  - `Bookings` table: booking records
- `Models/Booking.cs` + `Utilities/Booking.cs`
  - Defines booking fields and helper logic:
    - overlap check (`IsOverlapping`)
    - state constants
    - Telegram message formatting

### API endpoints (`Controllers/MainController.cs`)

Auth and identity:

- `GET /api/authenticate` (anonymous): serves login page
- `GET /api/challenge-ms` (anonymous): challenge Microsoft login
- `GET /api/challenge-ya` (anonymous): challenge Yandex login
- `GET /api/signin` (anonymous): redirects to login page endpoint
- `GET /api/signout`: clears cookie and redirects to login page
- `GET /api/self`: returns `{ name, email, isAdmin }`

Booking API:

- `GET /api/schedules?date=YYYY-MM-DD`
  - returns bookings for week (Monday -> Sunday) containing input date
- `POST /api/schedule`
  - server sets creator/name from authenticated user claims
  - rejects overlap for same room/date
  - sets state:
    - room 1 -> `ConfirmationWait`
    - other rooms -> `Booked`
- `PUT /api/confirm/{id}`
  - admin only
  - confirms room 1 booking in waiting state
- `DELETE /api/schedule/{id}`
  - allowed for creator or admin

Diagnostic endpoints:

- `GET /api/headers` (anonymous)
- `GET /api/claims`

## Authentication and identity mapping

- User identity is resolved from claims in this order:
  1. Yandex surname/givenname + email claim
  2. Generic `name` + `preferred_username`
- Admin role is derived from database `Admins` table lookup by email.

## Build/check status observed

### Frontend

- `npm run lint` failed because `eslint` command was not found in environment (dependencies not installed in current sandbox state).
- `npm run build` failed with TypeScript errors in current legacy source (existing issue).

### Backend

- `dotnet build` succeeded with warnings (nullable and unused fields/warnings).

## Risks and constraints to account for in the new version

- Sensitive secrets currently appear directly in backend appsettings (must move to secure secret/config management).
- Mixed authentication providers and custom claim mapping increase identity complexity.
- UI + backend are tightly coupled through PascalCase payloads and numeric room/state conventions.
- No dedicated test suite found in legacy frontend/backend folders.
- Some legacy code paths are commented out / partially inactive (notably Telegram internals).

