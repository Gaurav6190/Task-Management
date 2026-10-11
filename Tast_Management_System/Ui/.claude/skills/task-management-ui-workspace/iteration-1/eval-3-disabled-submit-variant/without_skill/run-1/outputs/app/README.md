# Task Management UI

React + TypeScript + Vite front end for `TaskManagement.Api`. Linted with ESLint (flat config, typescript-eslint, react-hooks, react-refresh).

## Getting started

```powershell
npm install
npm run dev      # http://localhost:5173
npm run lint
npm run build
```

Start the API first (`dotnet run --launch-profile https` in `Api/src/TaskManagement.Api`). The Vite dev server proxies `/api/*` to `https://localhost:7213`, so no CORS setup is needed in development. To point at another API, copy `.env.example` to `.env.local` and set `VITE_API_PROXY_TARGET` (e.g. `http://localhost:5008` for the `http` profile).

## Login

- `POST /api/auth/login` with `{ "username": "<email>", "password": "..." }` returns `{ accessToken, tokenType, expiresAtUtc, username }`.
- The username must be an email address (max 256 chars); the password is required (max 128 chars), matching the API's `LoginCommandValidator`.
- The **Sign in** button stays disabled until both fields are valid, and while a request is in flight.
- A `401` shows "Invalid username or password."; `400` validation errors are shown under the matching field.
- The token is kept in `sessionStorage` and dropped when it expires (60 minutes). Send it as `Authorization: Bearer <token>` on task API calls (`authorizationHeader()` in `src/auth/session.ts`).

## Layout

```
src/
  api/auth.ts              login() client + ApiError (ProblemDetails parsing)
  auth/session.ts          token storage / expiry helpers
  pages/LoginPage.tsx      login form
  pages/loginValidation.ts client-side rules mirroring the API validator
  App.tsx                  shows LoginPage or a signed-in placeholder
```
