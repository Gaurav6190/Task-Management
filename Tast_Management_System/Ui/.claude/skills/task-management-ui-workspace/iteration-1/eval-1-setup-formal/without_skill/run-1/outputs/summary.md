# Summary: Task Management UI setup (without skill)

## Steps

1. **Read the API (read-only)** at `C:\Work\Tast_Management_System\Api`:
   - `AuthController`: `POST /api/auth/login`, anonymous. Body `{ username, password }`. Returns `{ accessToken, tokenType, expiresAtUtc, username }`.
   - `LoginCommandValidator`: username required, max 256, must be an email address. Password required, max 128.
   - Errors are ProblemDetails plus `code`/`traceId`. A 400 has `errors.Username` / `errors.Password`. A 401 has code `Auth.InvalidCredentials`.
   - CORS allows only the origin `https://localhost:5173` (`appsettings.json`, `CorsTests`, task.md D20). The API's https profile is `https://localhost:7213`. The JWT lasts 60 minutes with no refresh token. Dev users are set with `scripts/Set-DevAuthUser.ps1` (Development only).
2. Node was installed but not on this shell's PATH, so added `C:\Program Files\nodejs` (Node 24.21.0, npm 11.19.0).
3. Scaffolded with `npm create vite@latest setup-formal-without_skill -- --template react-ts --no-interactive` (Vite 8.3, React 19.2, TypeScript 6.0), then ran `npm install`.
4. Switched the linter to ESLint. The current create-vite `react-ts` template ships oxlint, not ESLint. Removed `oxlint` and `.oxlintrc.json`, then installed `eslint`, `@eslint/js`, `typescript-eslint`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh` and `globals`. Added a flat `eslint.config.js` (the same setup as the classic Vite template) and set `"lint": "eslint ."`.
5. Matched the dev server to the API's CORS origin. Added `@vitejs/plugin-basic-ssl`, and `vite.config.ts` now runs on `https://localhost:5173` with `strictPort`. `.env.development` sets `VITE_API_BASE_URL=https://localhost:7213`.
6. Login page and supporting code:
   - `src/api/client.ts`: `postJson` helper, `ProblemDetails` type and `ApiError`. A network failure gives a friendly message.
   - `src/api/auth.ts`: request and response types that mirror the C# contracts, the length limits, and `login()`.
   - `src/auth/validation.ts`: client-side checks that match the API's validator.
   - `src/auth/session.ts`: keeps the token in `sessionStorage` and ignores it once it has expired.
   - `src/pages/LoginPage.tsx` / `.css`:
     - Email field (`type="email"`, `autoComplete="username"`) and Password field.
     - Errors show on each field when it loses focus and when the form is submitted.
     - A 400 from the server shows on the field; a 401 shows "Invalid email or password."
     - While signing in, the button and fields are disabled.
     - Accessible (labels, `aria-invalid`, `role="alert"`) and supports light and dark mode.
   - `src/App.tsx`: shows the login page when nobody is signed in, otherwise a "Signed in as …" placeholder with Sign out.
   - Cleanup: removed the template's demo assets, set the title to "Task Management" and the package name to `task-management-ui`, and rewrote the README.
7. Verified:
   - `npm run lint` and `npm run build` both exit 0.
   - The dev server returned 200 on `https://localhost:5173/` and was then stopped; nothing is listening on 5173.
   - A real login against the running API was not tested, because it needs the API, SQL Server and a dev user.
8. Copied the project to `outputs\app\` without `node_modules`, `dist` and `package-lock.json`.

## `npm run lint` (final output)

```
> task-management-ui@0.0.0 lint
> eslint .
```
(exit code 0, no warnings or errors)

## `npm run build` (final output)

```
> task-management-ui@0.0.0 build
> tsc -b && vite build

vite v8.3.4 building client environment for production...
✓ 23 modules transformed.
dist/index.html                   0.46 kB │ gzip:  0.30 kB
dist/assets/index-DJ-jjEEo.css    2.04 kB │ gzip:  0.83 kB
dist/assets/index-CQkupOnh.js   223.69 kB │ gzip: 70.17 kB
✓ built in 1.30s
```
(exit code 0)

## Message to the user

The Task Management UI is set up as a Vite + React + TypeScript app linted with ESLint. It starts on a login page with an Email field and a Password field. Lint and build both pass, but a real login against the API has not been tested yet.

- **ESLint, not the template default:** the current Vite `react-ts` template ships oxlint, so it was swapped for ESLint. `npm run lint` runs ESLint.
- **Email-only username:** the username field only accepts an email address. The page uses the same rules as the API's `LoginCommandValidator` (required, valid email, at most 256 characters; password at most 128). Errors show under each field.
- **Login call:** the page posts `{ username, password }` to `POST /api/auth/login` on `AuthController`. If the API rejects a field, the error shows on that field. Wrong credentials show "Invalid email or password." without saying which one was wrong. If the API can't be reached, the page says so.
- **Token:** the returned token is stored in `sessionStorage` and dropped when it expires (60 minutes; the API has no refresh token). After signing in, a placeholder screen shows "Signed in as …" with a Sign out button, until the task pages are built.
- **HTTPS on port 5173:** the API only accepts browser calls from `https://localhost:5173`, so the dev server runs on that exact address with a self-signed certificate. It calls the API at `https://localhost:7213`, which you can change with `VITE_API_BASE_URL` in `.env.development`.

To try it:
1. Start the API with the `https` profile.
2. If you haven't already, set up a dev user with `Api/scripts/Set-DevAuthUser.ps1`.
3. Run `npm run dev` and open https://localhost:5173. Accept the certificate warning once.

The first login may need the .NET dev certificate trusted (`dotnet dev-certs https --trust`).
