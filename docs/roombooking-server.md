# RoomBookingServer Documentation

## Overview

RoomBookingServer is an ASP.NET Core 8 Blazor Server application that provides an internal room-booking calendar for Interlink. It replaces the legacy React + ASP.NET Core Web API stack described in `legacy-overview.md`.

The server uses:
- **Blazor Server** (interactive server-side rendering) for the main UI
- **Minimal API** endpoints for file upload and download
- **Cookie authentication** backed by the Attendance SQL Server database
- **EF Core + SQL Server** for room bookings and booking documents
- **Local file system** for uploaded document storage

---

## Architecture

```
Browser
  │
  ├── Blazor circuit (SignalR) ──► Home.razor (interactive UI)
  │
  └── HTTP requests ──► Minimal API endpoints (auth, file upload/download)
                  │
                  ├── AttendanceContext  (Attendance DB)
                  └── RoombookingContext (Roombooking DB)
                              │
                              └── BookingDocuments → disk (booking-documents/)
```

---

## Configuration (`appsettings.json`)

| Key | Description |
|-----|-------------|
| `ConnectionStrings:Attendance` | Connection string for the Attendance SQL Server database (used to validate login credentials). |
| `ConnectionStrings:Roombooking` | Connection string for the Roombooking SQL Server database (bookings and documents). |
| `DocumentUpload:MaxFileSizeMB` | Maximum size per uploaded file, in **megabytes**. Default: `25`. |
| `DocumentUpload:MaxFileCount` | Maximum number of files that can be attached to a single booking. Default: `10`. |

### Example

```json
{
  "ConnectionStrings": {
    "Attendance":   "Server=...;Database=Attendance;...",
    "Roombooking":  "Server=...;Database=Roombooking;..."
  },
  "DocumentUpload": {
    "MaxFileSizeMB": 25,
    "MaxFileCount":  10
  }
}
```

The `DocumentUpload` settings are bound to `RoomBookingServer.Options.DocumentUploadOptions` via the standard ASP.NET Core options pattern. They are enforced in both:
- the Blazor UI (`Home.razor`) — before files are even sent to the server, and
- the upload API endpoint (`POST /api/bookings/{id}/documents`) — as a second guard on the server side.

---

## Authentication

Authentication is cookie-based.

**Login flow**:
1. User submits employee ID + password via `POST /auth/login`.
2. The server looks up the employee in the Attendance database (`AttendanceContext.Employees`).
3. If found and the password matches, it checks whether the employee is an admin by querying `RoombookingContext.Admins`.
4. A cookie is issued containing the employee ID, full name, optional email, and optionally the `Admin` role claim.
5. The cookie is valid for **8 hours** with sliding expiration.

**Logout**: `GET /auth/logout` signs out and redirects to `/login`.

**Authorization**: All pages and API endpoints require an authenticated cookie. The Blazor home page uses `@attribute [Authorize]`. Admin-only operations (booking confirmation, viewing all bookings' documents) check for the `Admin` role claim.

---

## Rooms and Booking States

Three rooms are supported:

| Room ID | Label |
|---------|-------|
| 1 | R1 (requires admin confirmation) |
| 2 | R2 |
| 3 | R3 |

Booking states (`Booking.State`):

| Value | Constant | Meaning |
|-------|----------|---------|
| `0` | `Booking.Booked` | Confirmed / active booking |
| `1` | `Booking.ConfirmationWait` | Waiting for admin confirmation (Room 1 only) |

When a booking is created for **Room 1**, its state is automatically set to `ConfirmationWait`. An admin must explicitly confirm it. Bookings for Rooms 2 and 3 are immediately `Booked`.

---

## API Endpoints

### Auth

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/auth/login` | Anonymous | Validate credentials; set session cookie. |
| `GET`  | `/auth/logout` | Any | Clear cookie; redirect to login. |

### Booking Documents

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET`  | `/api/bookings/{bookingId}/documents` | Required | List non-deleted documents for a booking. |
| `GET`  | `/api/bookings/{bookingId}/documents/{documentId}` | Required (owner only) | Stream a document file for download. Only the booking creator can download. |
| `POST` | `/api/bookings/{bookingId}/documents` | Required (owner or admin) | Upload one or more files (multipart/form-data) and attach them to a booking. Enforces `DocumentUpload` limits. |

---

## Document Lifecycle

### Upload

Documents are attached to a booking at two points:
- **Creating a new booking**: pending files are saved to disk after the booking row is inserted in the database.
- **Editing an existing booking**: new files in the editor are saved to disk when the user confirms the edit.

Each uploaded file is:
1. Written to `{ContentRoot}/booking-documents/` with a generated name in the format `{timestamp}-{randomHex}{extension}`.
2. A `BookingDocument` row is inserted in the `Roombooking` database with the original file name, stored file name, storage path, content type, size, upload time, and an `ExpiresAtUtc` timestamp.
3. Soft-deleted status (`DeletedAtUtc`) starts as `NULL` (file is active).

### Download

Only the booking creator can download attached documents via `GET /api/bookings/{id}/documents/{docId}`. The server reads the file from disk and streams it back using the original file name.

### Deletion During Booking Edit

When a user removes a document while editing a booking:
1. The file is deleted from disk (`BookingDocumentStorage.DeleteAsync`).
2. The `BookingDocument` row is deleted from the database.

If deletion fails, the save operation is aborted with an error.

### Deletion When a Booking Is Deleted

When a booking is deleted:
1. All non-deleted `BookingDocument` rows for that booking are fetched.
2. Each file is deleted from disk.
3. All `BookingDocument` rows are removed from the database.
4. The `Booking` row itself is deleted.

If any file deletion fails, the operation is aborted with an error and the booking is not deleted.

### Server Startup / Shutdown (Ephemeral Storage Policy)

**`BookingDocumentCleanupHostedService`** runs on both startup and shutdown:
- All files under `{ContentRoot}/booking-documents/` are deleted from disk (the directory is recreated empty).
- All `BookingDocument` rows are deleted from the database.

> **This means uploaded documents are ephemeral**: they do not survive a server restart. Documents are only intended to be accessible during the lifetime of the server process (e.g., during the booking meeting day or active session). This design is intentional for the current deployment model.

The `ExpiresAtUtc` column in `BookingDocument` was scaffolded for potential future use (e.g., a scheduled cleanup of old files without a full purge), but no scheduled expiry-based cleanup is currently implemented.

### Summary Table

| Event | Files on Disk | DB rows |
|-------|--------------|---------|
| File uploaded | Created in `booking-documents/` | `BookingDocument` inserted |
| File removed from editor | Deleted | `BookingDocument` deleted |
| Booking deleted | All booking files deleted | All `BookingDocument` rows deleted; `Booking` row deleted |
| Server starts | All files deleted (directory wiped) | All `BookingDocument` rows deleted |
| Server stops | All files deleted (directory wiped) | All `BookingDocument` rows deleted |

---

## Blazor UI (`Home.razor`)

The main page renders a week-view calendar. Users can:
- Navigate weeks (previous / current / next).
- Click an empty slot to create a new booking.
- Click an existing booking to view details, edit (if owner), delete (if owner or admin), or confirm (admin only, Room 1).

### Booking Editor

The booking editor dialog includes:
- Title, date, room, start/end time, optional note.
- A document upload panel where files can be dragged-and-dropped or selected via file picker.
- Existing documents attached to the booking are listed and can be removed.
- Pending uploads are listed with a progress indicator while saving.

File limits shown in the UI are read from the injected `IOptions<DocumentUploadOptions>` and reflect the live configuration values.

---

## File Storage Service (`BookingDocumentStorage`)

`IBookingDocumentStorage` / `BookingDocumentStorage` (scoped DI):

| Method | Description |
|--------|-------------|
| `SaveAsync` | Writes a stream to disk; returns metadata including the generated storage path. |
| `DeleteAsync` | Deletes a file by storage path; validates the path stays inside the root directory. |
| `OpenReadAsync` | Opens a file for streaming; validates path. Returns `null` if missing or out of bounds. |
| `CleanupAllAsync` | Deletes the entire storage directory and all database rows (used on startup/shutdown). |

The root storage path is `{IWebHostEnvironment.ContentRootPath}/booking-documents/`. Path traversal is guarded: any path that resolves outside this root is rejected silently.

---

## Project Structure

```
RoomBookingServer/
├── Components/
│   ├── App.razor                  # HTML shell, script/style references
│   ├── Routes.razor
│   ├── _Imports.razor
│   ├── Layout/
│   │   └── MainLayout.razor(.css)
│   └── Pages/
│       ├── Home.razor(.css)       # Main calendar + editor UI
│       ├── Login.razor(.css)
│       └── Error.razor
├── Models/
│   └── Db/
│       ├── Attendance/
│       │   ├── AttendanceContext.cs
│       │   └── Employee.cs
│       └── Roombooking/
│           ├── Admin.cs
│           ├── Booking.cs
│           ├── Booking.Extensions.cs
│           ├── BookingDocument.cs
│           └── RoombookingContext.cs
├── Options/
│   └── DocumentUploadOptions.cs   # Configurable upload limits
├── Services/
│   ├── BookingDocumentStorage.cs  # File I/O + DB cleanup
│   └── BookingDocumentCleanupHostedService.cs
├── wwwroot/
│   ├── app.css
│   ├── app.js                     # Dropzone drag-and-drop logic
│   └── …
├── Program.cs                     # DI setup, auth, Minimal API endpoints
└── appsettings.json
```
