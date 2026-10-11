# Summary – casual-with-tasks-page (without skill)

## Steps
1. Read the API (read-only) to learn the auth contract:
   - `POST /api/auth/login`, body `{ username, password }`, returns `{ accessToken, tokenType, expiresAtUtc, username }`.
   - Username must be an email (max 256); password max 128.
   - Errors are ProblemDetails with `code`, `detail` and `errors`. Bad credentials give 401 "Invalid username or password."
   - CORS only allows `https://localhost:5173`. The API's https profile is `https://localhost:7213`, and http on 5008 redirects to https.
   - The JWT lasts 60 minutes and there is no refresh token.
2. Scaffolded with `npm create vite@latest -- --template react-ts`. The current template ships oxlint, so removed it and set up ESLint (flat config: @eslint/js, typescript-eslint, react-hooks, react-refresh, globals) with `"lint": "eslint ."`.
3. Added react-router-dom and @vitejs/plugin-basic-ssl. The dev server is pinned to `https://localhost:5173` (strictPort) to match the API's CORS origin. `VITE_API_BASE_URL` sets the API URL and defaults to `https://localhost:7213`; `.env.example` is included.
4. Built the app:
   - `src/api/client.ts`: fetch wrapper that turns ProblemDetails into readable messages and reports when the API can't be reached.
   - `src/api/auth.ts`: `login()`.
   - `src/auth/*`: AuthProvider and useAuth (session in sessionStorage, automatic sign-out when the token expires), a RequireAuth guard, and email validation helpers.
   - `src/pages/LoginPage.tsx`: the email field strips whitespace as you type or paste and checks for a valid email. It shows an inline error, and the submit button stays disabled until the email is valid and a password is entered. Max lengths match the API, and API errors appear in an alert.
   - `src/pages/TasksPage.tsx`: protected, empty Tasks page with the user's email and a Log out button.
   - Routes: `/login`, `/tasks` (guarded), and everything else redirects to `/login`. Template boilerplate removed; light/dark CSS added.
5. Ran lint and build, both clean. Smoke-tested the dev server (200 on `/login`), then stopped it. No real login was tested because the API wasn't running.

## npm run lint
```
> task-management-ui@0.0.0 lint
> eslint .
```
(exit 0, no warnings or errors)

## npm run build
```
> task-management-ui@0.0.0 build
> tsc -b && vite build

vite v8.3.4 building client environment for production...
✓ 34 modules transformed.
dist/index.html                   0.46 kB │ gzip:  0.29 kB
dist/assets/index-CWGyUrCH.css    2.43 kB │ gzip:  0.96 kB
dist/assets/index-DlVnItv1.js   264.09 kB │ gzip: 84.08 kB
✓ built in 1.66s
```
(exit 0)

## Message to the user
The React frontend is set up (Vite + React 19 + TypeScript, linted with ESLint). `npm run lint` and `npm run build` both pass with no errors.

- **Login (first screen):** the username field only takes an email. Spaces are stripped, the email is checked, and **Sign in** stays disabled until the email is valid and a password is entered. It calls `POST /api/auth/login` and shows the API's error message (e.g. "Invalid username or password.").
- **After login:** an empty Tasks page with your email in the header and a **Log out** button. `/tasks` is protected, so you're sent back to login if you're not signed in. You're signed out automatically when the 60-minute token expires.

**How to run it:**
1. Start the API with the **https** profile (`https://localhost:7213`).
2. Run `npm install`, then `npm run dev`, and open **https://localhost:5173**. The dev server uses HTTPS on that exact port because the API's CORS only allows that origin. Accept the self-signed certificate warning once.
3. If the API runs on a different URL, copy `.env.example` to `.env.local` and set `VITE_API_BASE_URL`.

**Notes:**
- The current Vite template ships oxlint instead of ESLint, so it was replaced with an ESLint flat config.
- A real login could not be tested because the API wasn't running. The browser also has to trust the API's HTTPS dev certificate (`dotnet dev-certs https --trust`).
