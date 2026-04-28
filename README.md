# Appointment & Reservation System

Multi-role appointment booking platform built with ASP.NET Core MVC (.NET 8), SQL Server, and ADO.NET (no ORM). Three-layer architecture: `AppointmentSystem.Data` → `AppointmentSystem.Service` (namespace `AppointmentSystem.Bll`) → `AppointmentSystem.Web`.

---

## Features

### Roles
| Role | Capabilities |
|---|---|
| **Admin**    | Manage users (assign roles, disable accounts), manage service catalog, view dashboard with utilization stats |
| **Provider** | Define working hours / time-off, manage profile & offered services, confirm / complete / cancel appointments |
| **Customer** | Browse providers, book appointments, cancel own bookings, leave 1–5 star ratings on completed appointments |

### Booking flow
1. Customer picks a provider → service → date.
2. Browser fetches `/api/availability?providerId=...&serviceId=...&date=...` (AJAX) — JSON list of slots with `available: true/false`.
3. Customer clicks a slot → submits the form.
4. Server runs a **`SERIALIZABLE` transaction** with `(UPDLOCK, HOLDLOCK)`:
   - Re-checks for any overlapping `Pending`/`Confirmed` appointment on that provider.
   - If found → throws `BookingConflictException`, transaction rolls back, user sees error.
   - Otherwise inserts the row and commits.
5. A unique filtered index `UX_Appt_Provider_Start_Active(ProviderId, StartUtc) WHERE Status IN ('Pending','Confirmed')` provides a defense-in-depth guarantee at the schema level.
6. Notifications (in-DB log) created for both parties.

### Status flow
```
Pending ─→ Confirmed ─→ Completed
   │           │
   └─→ Cancelled ←─┘
```
- Provider can: confirm a Pending, complete a Confirmed, cancel anytime.
- Customer can: cancel a Pending or Confirmed (their own only).
- Admin can: change to any status.

### Calendar UI
- Weekly grid (Mon–Sun × 11 hours) rendered server-side from a partial view (`_WeekCalendar.cshtml`) — no external calendar library.
- Events are positioned within hour cells via inline `top`/`height` styles computed from minutes.
- Color-coded by status (Pending = amber, Confirmed = blue, Completed = green, Cancelled = grey).

### Notifications
Stored in DB as a journal table. Layout queries unread count and shows a badge on the bell icon. Visiting `/Notifications` marks all as read.

### Ratings
1–5 stars + optional comment, one per appointment, only by the customer who booked it, only after the appointment is `Completed`. Provider averages displayed on listing pages and on the dashboard.

---

## Architecture

```
AppointmentSystem/
├── Database/
│   ├── 001_Schema.sql            ← tables, indexes, constraints (incl. filtered unique idx)
│   ├── 002_StoredProcedures.sql  ← sp_GetProviderUtilization, sp_GetDailyAppointmentCounts
│   └── 003_SeedData.sql          ← demo users (admin/provider/customer), services, working hours, sample appointments
└── src/
    ├── AppointmentSystem.Data/        (assembly: AppointmentSystem.Data)
    │   ├── DbConnectionFactory.cs
    │   ├── Entities/                 ← POCOs mirroring DB rows
    │   └── Repositories/
    │       └── Interfaces/           ← all repos behind interfaces for DI
    ├── AppointmentSystem.Service/     (assembly: AppointmentSystem.Service, namespace: AppointmentSystem.Bll)
    │   ├── Services/
    │   │   └── Interfaces/
    │   ├── DTOs/                     ← AuthResult, TimeSlot, BookingResult
    │   └── Helpers/                  ← PasswordHasher (HMAC-SHA512)
    └── AppointmentSystem.Web/
        ├── Controllers/              ← Account, Home, Appointments, Provider, Admin, Notifications
        ├── Views/                    ← Razor + partials (`_Layout`, `_WeekCalendar`)
        ├── ViewModels/
        ├── wwwroot/
        │   ├── css/site.css          ← calendar + slot grid + star widget
        │   └── js/
        │       └── booking.js        ← AJAX slot picker + selection
        ├── Program.cs
        └── appsettings.json
```

> **Naming note**: the assembly is `AppointmentSystem.Service` (singular, project name) but its root namespace is `AppointmentSystem.Bll` to avoid a collision with the `Service` entity class name. All `using` statements reference `AppointmentSystem.Bll.*`.

Project references enforce direction: **Web → Service → Data**. The Data layer has no knowledge of MVC; the Service layer has no knowledge of HTTP. Controllers are thin — all business logic lives in services.

---

## Database schema (ASCII)

```
┌──────────┐         ┌──────────────────┐
│  Users   │◄────────│ ProviderProfiles │ (1:1, optional)
│  Id PK   │         │  UserId PK/FK    │
│  Role    │         │  Bio, Specialty  │
└─────┬────┘         └──────────────────┘
      │
      │ 1
      │
      │ M ┌─────────────────────┐    ┌────────────────┐    ┌──────────┐
      ├──►│ Appointments        │◄───┤ MovementItems? │    │ Services │
      │   │  ProviderId   FK───►│ no — (this is appt)│    │  Id PK   │
      │   │  CustomerId   FK───►│                    │    └────┬─────┘
      │   │  ServiceId    FK───►├────────────────────┘         │
      │   │  StartUtc, EndUtc   │                              │
      │   │  Status, Notes      │                              │
      │   └────┬────────────────┘                              │
      │        │ 1                                             │
      │        │                                               │
      │        │ 1                  ┌──────────────┐          │
      │        ├───────────────────►│ Ratings      │◄── 1     │
      │        │ (UNIQUE)           │ AppointmentId│          │
      │        │                    │ Stars, Cmt   │          │
      │        │                    └──────────────┘          │
      │   M    │                                              │
      ├────────┴────────┐                                     │
      │                 │                                     │
      │ ┌─────────────┐ │  ┌──────────┐   ┌────────────────┐ │
      ├►│ Notifications│ │  │ TimeOff  │   │ ProviderServ.  │◄┘
      │ │  UserId  FK │ │  │ ProviderFK│   │ Provider+Svc M:N │
      │ └─────────────┘ │  └──────────┘   └────────────────┘
      │                 │
      │ ┌──────────────┐│
      └►│ WorkingHours ││  recurring weekly intervals,
        │ ProviderId FK││  StartMinutes / EndMinutes from midnight
        │ DayOfWeek 0-6││  (multiple rows per day → breaks)
        └──────────────┘
```

### Critical indexes
- `IX_Appt_Provider_Range (ProviderId, StartUtc, EndUtc) INCLUDE (Status)` — fast overlap lookup at booking time
- `IX_Appt_Customer_Start (CustomerId, StartUtc DESC)` — customer's appointment list
- `UX_Appt_Provider_Start_Active (ProviderId, StartUtc) WHERE Status IN ('Pending','Confirmed')` — schema-level race protection
- `IX_TO_Provider_Range (ProviderId, StartUtc, EndUtc)` — time-off overlap lookup during slot generation

---

## Setup

### 1. Database (one-time)

```bash
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/001_Schema.sql
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/002_StoredProcedures.sql
sqlcmd -S "(localdb)\mssqllocaldb" -i Database/003_SeedData.sql
```

> Schema script drops & recreates `AppointmentDb` if it exists. All scripts include `SET QUOTED_IDENTIFIER ON` (required for the filtered unique index to work).

### 2. Run

```bash
cd src/AppointmentSystem.Web
dotnet run
```

Opens on `http://localhost:5077` (or whichever port `launchSettings.json` picks).

### 3. Demo accounts

All seed users have password **`password123`**:

| Email | Role | Notes |
|---|---|---|
| `admin@appt.test`   | Admin    | Full access |
| `smith@appt.test`   | Provider | Dr. Sarah Smith — General Medicine, Mon–Fri 9–5 with lunch break |
| `lee@appt.test`     | Provider | Dr. James Lee — Dentistry, Tue–Sat 10–6 |
| `alice@appt.test`   | Customer | Has one upcoming and one past (rated) appointment |
| `bob@appt.test`     | Customer | Has one pending appointment |

---

## Routes

| Path | Auth | Description |
|---|---|---|
| `/` | All authenticated | Customer landing (provider list + own appts) — providers and admins are redirected |
| `/book/{providerId}` | All authenticated | Booking page with slot picker + week calendar |
| `/api/availability` | All authenticated | AJAX JSON: returns slot list `{ startUtc, endUtc, available, label }` |
| `/Appointments/Details/{id}` | Owner / Admin | Detail view + status actions + rating form |
| `/Provider/Dashboard` | Provider / Admin | Weekly schedule + stats |
| `/Provider/Schedule` | Provider / Admin | Working hours, time off, profile, offered services |
| `/Admin` | Admin | Dashboard with daily counts + utilization (uses stored procs) |
| `/Admin/Users` | Admin | User management |
| `/Admin/Services` | Admin | Service catalog management |
| `/Notifications` | All authenticated | In-app notification log |

---

## Security

- Passwords hashed with **HMAC-SHA512** + per-user 64-byte salt
- Cookie auth, HTTP-only, 14-day sliding expiration
- Anti-forgery tokens on every state-changing form
- All SQL parameterized — no string concatenation
- Booking transaction at `SERIALIZABLE` isolation with explicit `(UPDLOCK, HOLDLOCK)`
- Unique filtered index as defense-in-depth against double-booking races
- Row-level authorization checks in services (e.g. customer can only rate own appointment)

---

## Screenshots placeholder

> _Add your own once running:_
>
> ![Customer landing](docs/screenshots/customer-home.png)
> ![Booking flow](docs/screenshots/booking.png)
> ![Provider dashboard](docs/screenshots/provider-dashboard.png)
> ![Admin dashboard](docs/screenshots/admin-dashboard.png)
