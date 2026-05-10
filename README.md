# Resume Automation

[![CI](https://github.com/Giorgi2201/resume-yourself/actions/workflows/ci.yml/badge.svg)](https://github.com/Giorgi2201/resume-yourself/actions/workflows/ci.yml)

A full-stack AI-powered resume screening platform. Upload CVs against a job description, get instant ranked scores with keyword analysis, and leave hiring feedback — all behind a secure, per-user authentication system.

**Stack:** Angular 19 (SSR) · ASP.NET Core 10 · SQLite/PostgreSQL · Entity Framework Core · JWT + httpOnly Refresh Token Cookies · Docker

---

## Features

### Authentication
- **Registration** with email + password (Identity password rules enforced)
- **Email verification** — account is locked until the link is clicked (24-hour expiry, single-use token)
- **Login** gated on verified email; clear error + "Resend verification" button on login page
- **JWT access tokens** (15-min, in-memory only) + **httpOnly cookie refresh tokens** (14-day, SHA-256 hashed in DB, rotation on use)
- **Rate limiting** on auth endpoints (10 req/min) and upload endpoints (30 req/min)
- **Security headers** via NWebsec: HSTS, CSP, X-Frame-Options, X-XSS-Protection, Referrer-Policy
- **Account lockout** after 5 failed login attempts (15-min lockout)

### Data isolation
Every user sees **only their own data**. `UserId` is stamped on every job at creation time from the JWT claim — never accepted from the client. All queries, uploads, result lookups, and feedback operations are filtered by `UserId`. Accessing another user's resource returns 404.

### Resume Screening
- Create job postings with a description
- Upload CVs (PDF, DOCX, TXT) — bulk upload supported (max 20 files, 5MB each)
- Automatic text extraction, skill identification, and keyword scoring
- **Configurable scoring weights** via `ScoringOptions` (core skills, role similarity, experience, nice-to-have)
- Candidates ranked by match score against the job description
- Core vs secondary keyword breakdown, hard filter flags, score explanations
- Leave Approved / Rejected feedback per candidate per job
- Duplicate CV detection per job (SHA-256 content hash)

### Frontend
- Angular 19 standalone components with SSR (Angular Universal)
- **Secure token storage** — access token in memory only, refresh token via httpOnly cookie (XSS-safe)
- Auth interceptor — attaches Bearer token, silently refreshes on 401 via cookie, retries original request
- Route guards (`authGuard` / `guestGuard`)
- Register → verify email → login flow with clean UX states
- **jwt-decode** library for client-side token expiration checks

---

## Project structure

```
resume-automation/
├── Resume.Api/                  # ASP.NET Core 10 Web API
│   ├── Configuration/           # Options classes (Jwt, Email, Cors, FileUpload, InitialAdmin, Scoring)
│   ├── Controllers/             # Auth, Jobs, Candidates, Results, Feedback
│   ├── Data/                    # AppDbContext, IdentityDataSeeder, DateTimeOffsetUtcTicksConverters
│   ├── DTOs/                    # Request / response records (immutable IReadOnlyList)
│   ├── Exceptions/              # ApiException, FileParsingException
│   ├── Extensions/              # ClaimsPrincipalExtensions (GetUserId)
│   ├── Middleware/              # ExceptionHandlingMiddleware
│   ├── Migrations/              # EF Core migrations (PostgreSQL snapshotted)
│   ├── Models/                  # ApplicationUser, Job, Candidate, CandidateScore, Feedback, AuditLog, RefreshToken
│   └── Services/                # Auth, Email, CvParser, Scoring, Upload, Audit, RefreshTokenCleanup
├── src/                         # Angular 19 frontend
│   ├── app/
│   │   ├── guards/              # authGuard, guestGuard
│   │   ├── pages/               # landing, login, register, verify-email, screenings, upload, results
│   │   ├── services/            # AuthService (secure token storage), ApiService, authInterceptor
│   │   └── models/              # TypeScript interfaces
│   └── environments/            # environment.ts, environment.prod.ts
├── docker-compose.yml           # Full stack orchestration (PostgreSQL, API, nginx)
├── Resume.Api/Dockerfile        # Multi-stage API build
├── Dockerfile.frontend          # Multi-stage Angular + nginx build
└── nginx.conf                   # Reverse proxy + SPA routing configuration
```

---

## Getting started

### Prerequisites (Local Development)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- Docker & Docker Compose (optional, for containerized deployment)

### 1. Clone and install

```bash
git clone https://github.com/Giorgi2201/resume-yourself.git
cd resume-yourself
npm install
```

### 2. Configure the backend

The API reads secrets from `appsettings.Development.json` (git-ignored in production, use environment variables or user secrets for prod).

**DateTimeOffset SQLite Support:** The API uses `DateTimeOffsetUtcTicksConverters` to store timestamps as UTC ticks (bigint) for reliable SQLite comparisons. This avoids EF Core translation errors with DateTimeOffset on SQLite.

**Minimum required — `Resume.Api/appsettings.Development.json`:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=resume_yourself_dev.db"
  },
  "Jwt": {
    "Key": "your-secret-key-minimum-32-characters!!",
    "ExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 14
  },
  "Email": {
    "SmtpHost": "",
    "SmtpPort": 587,
    "SmtpUser": "",
    "SmtpPassword": "",
    "UseSsl": true,
    "FromAddress": "you@example.com",
    "FromName": "Resume Automation",
    "AppBaseUrl": "http://localhost:4200"
  },
  "InitialAdmin": {
    "Enabled": true,
    "Email": "admin@localhost.dev",
    "Password": "ChangeMe_Dev_123!",
    "Role": "Admin"
  }
}
```

> **No SMTP configured?** Leave `SmtpHost` blank. The verification link is logged to the API console at `warn` level — paste it in your browser to verify without a mail server.

> **Real email (Gmail):** set `SmtpHost: smtp.gmail.com`, `SmtpPort: 587`, `SmtpUser` / `SmtpPassword` to a Gmail [App Password](https://support.google.com/accounts/answer/185833), and `FromAddress` matching `SmtpUser`.

**Scoring Configuration (optional):**
```json
{
  "Scoring": {
    "BaseScore": 5,
    "CoreSkillsWeight": 65,
    "RoleSimilarityWeight": 10,
    "YearsExperienceWeight": 10,
    "SecondarySkillsWeight": 10
  }
}
```

### 3. Run the backend

```bash
cd Resume.Api
dotnet run
# API listens on http://localhost:5064
# Database is created and migrated automatically on first run
# Health check available at http://localhost:5064/health
```

### 4. Run the frontend

```bash
# From the repo root
ng serve --open
# App opens at http://localhost:4200
```

---

## Auth flow

```
Register (/register)
  └─ POST /api/auth/register
       ├─ Creates user (EmailConfirmed = false), assigns "User" role
       ├─ Generates 24-hour email confirmation token
       └─ Sends verification email (or logs link to console if SMTP not set)

Verify email (/verify-email?email=…&token=…)
  └─ POST /api/auth/verify-email
       └─ Confirms email — token is single-use and expires after 24 hours

  On expired/used link → "Resend verification email" button available

Login (/login)
  └─ POST /api/auth/login
       ├─ Rejects if email not verified (403 with clear message)
       ├─ Locks account after 5 bad attempts
       └─ Returns JWT (15 min) + refresh token (14 days)

Authenticated requests
  └─ authInterceptor attaches Bearer header
       └─ On 401 → refresh → retry original request
            └─ On refresh failure → clear session → redirect to /login
```

---

## Environment variables (production)

All secrets should come from environment variables in production — never commit real credentials.

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQLite (or other) connection string |
| `Jwt__Key` | Signing key — minimum 32 characters |
| `Jwt__Issuer` | Token issuer |
| `Jwt__Audience` | Token audience |
| `Email__SmtpHost` | SMTP server hostname |
| `Email__SmtpUser` | SMTP login |
| `Email__SmtpPassword` | SMTP password / app password |
| `Email__FromAddress` | Sender address |
| `Email__AppBaseUrl` | Frontend base URL for verification links |
| `InitialAdmin__Enabled` | `true` to seed an admin on first start |
| `InitialAdmin__Email` | Admin email |
| `InitialAdmin__Password` | Admin password |

---

## Database migrations

Migrations are **snapshotted for PostgreSQL** (correct identities and types). SQLite in development applies the same migration operations; the CLI uses `AppDbContextFactory` in `Data/AppDbContextFactory.cs`.

```bash
cd Resume.Api

# Apply pending migrations to the SQLite file from appsettings (also runs at startup via Migrate())
dotnet ef database update

# Add a new migration after model changes — must use Npgsql snapshot (no live DB required):
# PowerShell:
$env:EFCORE_PG_DESIGN = "1"
dotnet ef migrations add <MigrationName>
Remove-Item Env:EFCORE_PG_DESIGN

# bash:
# EFCORE_PG_DESIGN=1 dotnet ef migrations add <MigrationName>
```

Production (Docker) uses `ASPNETCORE_ENVIRONMENT=Production` and a PostgreSQL connection string; migrations run automatically on API startup.

---

## Docker Deployment

Run the full stack with Docker Compose:

```bash
# Create .env file or export variables
export POSTGRES_PASSWORD=your_secure_password
export JWT_KEY=your-minimum-32-character-secret-key
export FRONTEND_PORT=80

docker-compose up --build
```

**Services:**
- `db` — PostgreSQL 16 with health checks
- `api` — ASP.NET Core API with health checks (depends on db)
- `frontend` — nginx serving Angular SPA with API proxying

The compose file includes proper health checks, dependency ordering, and graceful startup.

---

## Tech notes

- **Password policy:** min 10 chars, requires uppercase, lowercase, digit, and symbol
- **Refresh token storage:** SHA-256 hashed in DB; httpOnly cookie for client; rotated on every use; old token revoked
- **Access token storage:** In-memory only — never persisted to localStorage (XSS protection)
- **Candidate deduplication:** SHA-256 hash of parsed CV text per job
- **Scoring:** keyword-based with configurable weights; core/secondary split and hard filter flags
- **DateTimeOffset handling:** Stored as UTC ticks (bigint) for SQLite compatibility
- **EF Core optimizations:** AsNoTracking on read queries, HashSet for lookups, CancellationToken propagation
- **Security middleware:** NWebsec with CSP, HSTS, frame/clickjacking protection
- **Health checks:** `/health` endpoint with database connectivity check
- **SSR:** Angular Universal — verification links are handled client-side only (no SSR token consumption)
