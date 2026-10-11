# Summary: disabled-submit-variant (without skill)

## Steps

1. **Read the API (read-only):**
   - `AuthController`: `POST /api/auth/login`.
   - `LoginRequest` and `LoginResponse` contracts.
   - `LoginCommandValidator`: username is required, at most 256 characters and an email; password is required, at most 128 characters.
   - `AuthErrors`: 401 returns "Invalid username or password."
   - Errors come back as ProblemDetails or ValidationProblemDetails.
   - `launchSettings.json`: https://localhost:7213 and http://localhost:5008.
   - `appsettings.json`: CORS allows only `https://localhost:5173`.
2. **Node:** Node was not on the shell PATH, so used `C:\Program Files\nodejs` (Node 24.21.0, npm 11.19.0).
3. **Scaffold:** ran `npx create-vite@latest <dir> --template react-ts --no-interactive` (Vite 8, React 19, TS 6), then `npm install`.
4. **Swapped oxlint for ESLint:** the current create-vite template ships oxlint, but ESLint was required.
   - Removed `oxlint` and `.oxlintrc.json`.
   - Installed `eslint`, `@eslint/js`, `typescript-eslint`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh` and `globals`.
   - Added a flat `eslint.config.js` and set `"lint": "eslint ."`.
   - Checked that ESLint actually lints all the source files.
5. **Configured `vite.config.ts`:**
   - Port 5173, with `strictPort`.
   - A dev proxy from `/api` to `https://localhost:7213`. It uses `secure: false` to accept the dev certificate and can be overridden with `VITE_API_PROXY_TARGET`.
   - The proxy avoids a CORS mismatch: the API allows `https://localhost:5173`, but Vite serves `http://localhost:5173`.
   - Added `.env.example` with `VITE_API_PROXY_TARGET` and `VITE_API_BASE_URL`.
6. **Implemented:**
   - `src/api/auth.ts`: the `login()` client, plus an `ApiError` carrying the status and field errors parsed from ProblemDetails. Network failures produce a friendly message.
   - `src/auth/session.ts`: keeps the token in `sessionStorage`, checks expiry, and provides an `authorizationHeader()` helper for later task calls.
   - `src/pages/loginValidation.ts`: client-side rules that mirror `LoginCommandValidator`.
   - `src/pages/LoginPage.tsx` and `LoginPage.css`: the email and password form.
     - **Sign in is disabled until both fields are valid**, and also while a request is in flight.
     - Field errors show after the user leaves a field.
     - A 400 shows the server's field errors under the matching fields.
     - A 401 shows an alert and clears the password.
     - Has accessible labels, aria attributes and autocomplete hints.
   - `src/App.tsx`: shows the login page, or a signed-in placeholder with a Sign out button. It signs the user out when the token expires.
   - Light and dark styling through CSS variables.
   - Removed unused template assets, renamed the package to `task-management-ui`, and rewrote the README.
7. **Checks:** ran `npm run lint` and `npm run build`; both pass. No dev server was started.

## `npm run lint`

```
> task-management-ui@0.0.0 lint
> eslint .

(exit code 0, no warnings or errors)
```

## `npm run build`

```
> task-management-ui@0.0.0 build
> tsc -b && vite build

vite v8.3.4 building client environment for production...
✓ 22 modules transformed.
dist/index.html                   0.47 kB │ gzip:  0.31 kB
dist/assets/index-DIirWQnp.css    2.25 kB │ gzip:  0.91 kB
dist/assets/index-B56Czm6P.js   224.12 kB │ gzip: 70.34 kB
✓ built in 774ms
```

## Message to the user

The Task Management UI is set up (Vite 8 + React 19 + TypeScript, ESLint) with the login page as the first screen.

- **Login:** email + password, posted to `POST /api/auth/login` as `{ username, password }`. Client-side rules match the API's `LoginCommandValidator` (valid email, max 256 chars; password required, max 128).
- **Your extra requirement:** the Sign in button stays disabled until both fields are valid, and also while the request is in flight so it can't be double-submitted.
- **Errors:** a 401 shows "Invalid username or password." and clears the password. A 400 shows the API's field messages under the matching inputs. If the API can't be reached, you get a "Cannot reach the server" message.
- **Session:** the JWT is kept in `sessionStorage`, and you're signed out automatically when it expires after 60 minutes. Use `authorizationHeader(session)` for task API calls.
- **API connection:** in dev, Vite proxies `/api` to `https://localhost:7213`, so start the API with the `https` profile and run `npm run dev`. Without the proxy the login would fail CORS, because the API only allows `https://localhost:5173` and Vite serves plain `http://localhost:5173`. For a deployed build, set `VITE_API_BASE_URL` and add that UI origin to the API's `Cors:AllowedOrigins`.
- **Lint and build:** `npm run lint` and `npm run build` both pass. A real sign-in against a running API was not tried, since no API or credentials were available.
- **ESLint swap:** the current Vite template ships oxlint, so it was replaced with ESLint as asked.
