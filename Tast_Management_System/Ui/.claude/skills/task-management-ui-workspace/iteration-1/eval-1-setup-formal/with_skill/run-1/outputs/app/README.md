# Task Management UI

React 19 + TypeScript + Vite, linted with ESLint. Signs in against **TaskManagement.Api**.

## Run it

1. Start the API with its `https` launch profile (`https://localhost:7213`).
2. In this folder:

   ```bash
   npm install
   npm run dev
   ```

3. Open http://localhost:5173. You'll land on `/login`.

| Script            | What it does                         |
| ----------------- | ------------------------------------ |
| `npm run dev`     | Dev server with `/api` proxied       |
| `npm run lint`    | ESLint                               |
| `npm run build`   | Type-check (`tsc -b`) and build      |
| `npm run preview` | Serve the production build           |

## How the UI talks to the API

- `POST /api/auth/login` with `{ username, password }` returns `{ accessToken, tokenType, expiresAtUtc, username }`.
- In development, Vite proxies `/api` to `API_PROXY_TARGET` (see `.env.development`), so the API's
  CORS list doesn't matter. Use `http://localhost:5008` there if you run the API's `http` profile.
- For a deployed build, set `VITE_API_BASE_URL` to the API's URL and add the UI's origin to the API's
  `Cors:AllowedOrigins`.
- The token is kept in `sessionStorage` and the user is signed out when it expires.
- Send it on task calls with `apiRequest(path, { token: session.accessToken })` from `src/api/httpClient.ts`.

## Login rules

The username field only accepts an email address: whitespace is stripped as you type, and the form
won't submit until the value is a valid address (max 256 characters). The password must be 1–128
characters. These limits match the API's `LoginCommandValidator`.

## Layout

```
src/
  api/        httpClient.ts (fetch + problem-details errors), authApi.ts
  auth/       AuthProvider, useAuth, RequireAuth, session storage, credential validation
  pages/      LoginPage, TasksPage (placeholder)
```
