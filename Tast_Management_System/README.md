# Task Management System

A task tracker with a **.NET 8 Web API** (Domain-Driven Design, EF Core, SQL Server) and a **React** front end
(Vite + TypeScript). Users log in, see a filterable task table, create tasks, and change status and priority inline.

There are two ways to run it:

- **[Option A: Docker](#option-a-run-with-docker-recommended)** (recommended). Everything runs in containers.
  You only need Docker Desktop.
- **[Option B: Local development](#option-b-local-development-without-docker)**. Run the API and UI directly on
  your machine, for coding and debugging.

---

## Default login

| Field    | Value                        |
|----------|------------------------------|
| Username | `admin@taskmanagement.local` |
| Password | `Test@123`                   |

These are **local development credentials**. They work only after you create them with the setup step
(Docker: [step 3](#3-create-the-configuration-file-env); local development: [step 3](#3-create-the-dev-login)).
The app is published on `127.0.0.1` only, so no other machine can reach it. Never use this login, or this
configuration, on a server that's reachable from a network.

---

## Project structure

```
Tast_Management_System/
├── Api/                                  .NET 8 Web API
│   ├── src/
│   │   ├── TaskManagement.Domain/          entities, value objects, domain rules
│   │   ├── TaskManagement.Application/     use cases, validation
│   │   ├── TaskManagement.Infrastructure/  EF Core, SQL Server, migrations, auth
│   │   └── TaskManagement.Api/             controllers, Program.cs, appsettings
│   ├── tests/                              unit, integration and architecture tests
│   ├── scripts/Set-DevAuthUser.ps1         creates a dev login for local development
│   ├── Dockerfile                          API image + migration image
│   └── TaskManagement.sln
├── Ui/                                   React + Vite + TypeScript
│   ├── src/
│   ├── nginx/default.conf.template         nginx config used in the Docker image
│   └── Dockerfile
├── docker-compose.yml                    SQL Server + migrations + API + UI
├── .env.example                          template for .env (settings and secrets)
├── New-DockerEnv.ps1                     creates .env and the login user
└── README.md
```

---

## Option A: Run with Docker (recommended)

### What runs

| Service     | What it does                                                        | Reachable at                       |
|-------------|---------------------------------------------------------------------|------------------------------------|
| `sqlserver` | SQL Server 2022 (Developer edition). Data kept in a Docker volume   | `localhost,14333` (SQL tools only) |
| `migrator`  | Creates/updates the database schema and seed data, then exits       | none                               |
| `api`       | .NET 8 Web API on port 8080                                         | internal only (through the UI)     |
| `ui`        | React app served by nginx. Forwards `/api/*` to the API             | **http://localhost:8080**          |

This SQL Server runs inside Docker and is **separate from any SQL Server installed on your PC**.
It uses an `sa` password, which the setup script generates for you. Windows authentication isn't possible
from Linux containers. Your local databases aren't affected.

### Prerequisites

| Requirement                 | Notes |
|-----------------------------|-------|
| Windows 10/11 64-bit        | Pro, Enterprise, Home and Workstations editions all work. macOS and Linux work too, apart from the PowerShell setup script. |
| Hardware virtualization     | Must be enabled in BIOS/UEFI (Intel VT-x / AMD-V). Check in Task Manager → Performance → CPU → "Virtualization: Enabled". |
| WSL 2                       | In an **administrator** PowerShell: `wsl --install --no-distribution`, then reboot. |
| Docker Desktop              | `winget install -e --id Docker.DockerDesktop` (approve the admin prompt), or download it from docker.com. Keep "Use WSL 2" selected. Reboot. |
| ~4 GB free RAM, ~5 GB disk  | SQL Server needs at least 2 GB of RAM. |
| Free ports                  | `8080` (UI) and `14333` (SQL tools). Both can be changed in `.env`. |

After installing, **start Docker Desktop** and wait for **"Engine running"** (bottom left of the Docker Desktop window).
Then open a **new** terminal and check:

```powershell
docker version
docker compose version
```

You don't need the .NET SDK, Node.js or SQL Server for Option A. Everything builds inside Docker.

### 1. Get the code

Copy or clone the project folder, for example to `C:\Work\Tast_Management_System`, and open a PowerShell terminal there:

```powershell
cd C:\Work\Tast_Management_System
```

### 2. Allow the setup script to run (current terminal only)

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

### 3. Create the configuration file (.env)

```powershell
./New-DockerEnv.ps1 -Email "admin@taskmanagement.local" -Password "Test@123"
```

This creates `.env` with:

- `MSSQL_SA_PASSWORD`: random SQL Server `sa` password (generated).
- `JWT_SIGNING_KEY`: random key used to sign login tokens (generated).
- `AUTH_USER_EMAIL` and `AUTH_USER_PASSWORD_HASH`: the login. The password is stored only as a hash.

`.env` contains secrets. **Don't commit it or share it.** Other settings (ports, image tag, environment) are
documented in [.env.example](.env.example).

### 4. Build and start

```powershell
docker compose up -d --build
```

The first run downloads base images and builds everything, which takes about 3 to 10 minutes. Check the status:

```powershell
docker compose ps
```

Expected result:

| SERVICE     | STATUS              |
|-------------|---------------------|
| `sqlserver` | Up (healthy)        |
| `migrator`  | Exited (0)          |
| `api`       | Up                  |
| `ui`        | Up (healthy)        |

`migrator` showing **Exited (0)** is correct. It runs once to set up the database, then stops.

### 5. Open the app

Go to **http://localhost:8080** and log in with the [default login](#default-login):
`admin@taskmanagement.local` / `Test@123`.

### Everyday Docker commands

```powershell
docker compose up -d                     # start (after a reboot or `down`)
docker compose down                      # stop and remove containers (data is kept)
docker compose down -v                   # stop AND delete the database
docker compose ps                        # status
docker compose logs -f api               # follow API logs (also: ui, sqlserver, migrator)
docker compose up -d --build             # rebuild after code changes
docker compose up -d --build ui          # rebuild only the UI
docker compose run --rm migrator         # apply new EF Core migrations
```

### Change the login

```powershell
./New-DockerEnv.ps1 -Email "someone@example.com" -Password "<new password>"
docker compose up -d
```

This keeps the existing SQL Server password and JWT key, so your data stays. Only the login changes.
To regenerate those secrets too, run `docker compose down -v` (**this deletes the data**), then run the script
with `-RegenerateSecrets`.

### Connect to the Docker database (optional)

In SSMS or Azure Data Studio:

- **Server:** `localhost,14333`
- **Authentication:** SQL Server Authentication
- **Login:** `sa`
- **Password:** the `MSSQL_SA_PASSWORD` value in `.env`
- **Options:** tick "Trust server certificate"

### How login works in Docker

The API runs in `Production` mode. Normally the API ignores configured logins outside `Development`.
`.env` turns them back on with `AUTH_ALLOW_CONFIGURED_USERS=true`.
That's safe only because the UI port is bound to `127.0.0.1`. If you ever publish the app on a network,
set it to `false` and use a real identity provider.

---

## Option B: Local development (without Docker)

Use this to run and debug the code in Visual Studio or VS Code with hot reload.

### Prerequisites

| Requirement             | Version / notes |
|-------------------------|-----------------|
| .NET SDK                | **8.0.425 or later 8.0.x** (pinned in `Api/global.json`). `winget install Microsoft.DotNet.SDK.8` |
| Node.js                 | **20.19+ / 22.12+ / 24.x** (Vite 8). `winget install OpenJS.NodeJS.LTS` |
| SQL Server              | Local default instance (`localhost`) with **Windows authentication**. Developer or Express edition. |
| IDE (optional)          | Visual Studio 2022 17.8+, VS Code, or JetBrains Rider |

Check:

```powershell
dotnet --version
node --version
```

### 1. Trust the HTTPS development certificate (once)

```powershell
dotnet dev-certs https --trust
```

### 2. Database

The API uses `Server=localhost;Database=TaskManagementDb;Trusted_Connection=True` (see
`Api/src/TaskManagement.Api/appsettings.json`). In Development, the database is **created and migrated
automatically** when the API starts. You don't need to create it yourself.

To apply migrations manually instead:

```powershell
cd Api
dotnet tool restore
dotnet ef database update --project src/TaskManagement.Infrastructure --startup-project src/TaskManagement.Api
```

### 3. Create the dev login

This stores the login and a JWT signing key in .NET **user-secrets** (outside the repository):

```powershell
cd Api
Set-ExecutionPolicy -Scope Process Bypass
./scripts/Set-DevAuthUser.ps1 -Username "admin@taskmanagement.local" -Password "Test@123"
```

### 4. Run the API

```powershell
cd Api
dotnet run --project src/TaskManagement.Api --launch-profile https
```

- API: https://localhost:7213 (also http://localhost:5008)
- Swagger: https://localhost:7213/swagger (Development only)

### 5. Run the UI (second terminal)

```powershell
cd Ui
npm install
npm run dev
```

Open **http://localhost:5173** and log in with `admin@taskmanagement.local` / `Test@123`.

The Vite dev server forwards `/api` to `https://localhost:7213` (set in `Ui/.env.development`).
If you run the API with the `http` profile, change `API_PROXY_TARGET` to `http://localhost:5008`.

### Tests and checks

```powershell
cd Api
dotnet test                 # all test projects
cd ../Ui
npm run lint
npm run build               # type-check + production build
```

If the API is already running from Visual Studio, `dotnet test` may fail with "file is locked". Stop the API,
or run `dotnet test -c Release`.

---

## API endpoints

All endpoints except login need an `Authorization: Bearer <token>` header.

| Method | Path                 | Description                         |
|--------|----------------------|-------------------------------------|
| POST   | `/api/auth/login`    | Log in with `{ username, password }` and get an `accessToken` |
| GET    | `/api/tasks`         | List tasks (paging and filters)     |
| GET    | `/api/tasks/summary` | Task counts                         |
| GET    | `/api/tasks/{id}`    | Get one task                        |
| POST   | `/api/tasks`         | Create a task                       |
| PUT    | `/api/tasks/{id}`    | Update a task (status, priority, …) |
| DELETE | `/api/tasks/{id}`    | Delete a task                       |

Quick test against the Docker setup:

```powershell
$t = (Invoke-RestMethod -Method Post http://localhost:8080/api/auth/login -ContentType application/json `
      -Body '{"username":"admin@taskmanagement.local","password":"Test@123"}').accessToken
Invoke-RestMethod http://localhost:8080/api/tasks -Headers @{ Authorization = "Bearer $t" }
```

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `docker` is not recognized | Start Docker Desktop, then open a **new** terminal. Docker adds itself to PATH at install time. |
| Docker Desktop: "WSL 2 is not installed" / "Virtualization not enabled" | Run `wsl --install --no-distribution` as administrator and reboot. Enable VT-x/AMD-V in BIOS. |
| `New-DockerEnv.ps1 cannot be loaded … running scripts is disabled` | Run `Set-ExecutionPolicy -Scope Process Bypass` in the same terminal. |
| `Set MSSQL_SA_PASSWORD in .env` (or `JWT_SIGNING_KEY`) | `.env` is missing or incomplete. Run `New-DockerEnv.ps1` (step 3). |
| Login says invalid email or password | `AUTH_USER_EMAIL`/`AUTH_USER_PASSWORD_HASH` in `.env` are empty or wrong. Re-run `New-DockerEnv.ps1`, then `docker compose up -d`. |
| `migrator` shows `Exited (1)` | `docker compose logs migrator`. Usually the SA password in `.env` doesn't match the existing volume (after `-RegenerateSecrets`). Either restore the old password, or run `docker compose down -v` (deletes data). |
| `sqlserver` keeps restarting | Give Docker at least 2 GB RAM (Settings → Resources). Check `docker compose logs sqlserver`. |
| Port 8080 or 14333 already in use | Set `UI_PORT` / `SQL_PORT` in `.env` to a free port, then `docker compose up -d`. |
| UI loads but shows "Unable to reach the server" | `docker compose ps`: is `api` up? Then `docker compose logs api`. |
| Local dev: API can't connect to SQL Server | Make sure the `MSSQLSERVER` Windows service is running and your Windows user can log in to it. |
| Local dev: browser warns about the certificate | `dotnet dev-certs https --trust`, then restart the browser. |

---

## Production notes

This setup is hardened for a **single machine on localhost**: non-root containers, a minimal API image,
a read-only API filesystem, secrets kept in `.env`, and the database not reachable from outside the machine.
Before deploying to a real server:

- Replace the configured login with a real identity provider and set `AUTH_ALLOW_CONFIGURED_USERS=false`.
- Put a TLS-terminating reverse proxy or load balancer in front of the `ui` service. The containers speak plain HTTP.
- Use a managed SQL Server or Azure SQL with a least-privilege login instead of `sa`, and remove the `sqlserver` service.
- Keep secrets in a secret store (Docker/Kubernetes secrets, Azure Key Vault) instead of `.env`.
- Tag and push images: `$env:TAG = "1.0.0"; docker compose build`, then push `task-management/*` to your registry.
