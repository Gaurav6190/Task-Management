# Summary: disabled-submit-variant (with skill)

## Steps
1. Read the skill's `SKILL.md` and `references/api-contract.md`.
2. Checked the contract against the API source (read only): `AuthController.cs`, `AuthContracts.cs` and `LoginCommand.cs`. It matches:
   - `POST /api/auth/login` with `{ username, password }`.
   - Username is required, at most 256 characters, and must be an email address.
   - Password is required and at most 128 characters.
   - The response is `{ accessToken, tokenType, expiresAtUtc, username }`.
3. `node` was not on PATH, so used `C:\Program Files\nodejs\node.exe` (v24.21.0).
4. Ran `node scripts/create-app.mjs <target> --name task-management-ui`. It ran create-vite@9 react-ts with ESLint, applied the template, installed packages including react-router@^8, then ran lint and build. All passed.
5. Added the extra requirement:
   - **`src/pages/LoginPage.tsx`:**
     - Added `fieldsValid`: the username passes `validateUsername`, the password passes `validatePassword`, and no API field error is left unedited.
     - Added `canSubmit = fieldsValid && !submitting`. The button uses `disabled={!canSubmit}` and sets `aria-busy` while signing in.
     - Because the submit button is disabled, pressing Enter in a field does nothing.
     - `handleSubmit` keeps its validation guard as a backup and ignores a second submit while one is running.
     - Added a hint under the button ("Enter a valid email address and your password to sign in."), linked with `aria-describedby`. A disabled button can't be focused and gives no feedback on its own.
   - **`LoginPage.module.css`:** the disabled button uses `cursor: not-allowed` and lower opacity. While signing in (`aria-busy`) it keeps `cursor: progress`. Added a `.submitHint` style.
   - **`README.md`:** one sentence describing the behaviour.
   - The rest of the template is unchanged: email-only username, whitespace stripping, errors shown after leaving a field, the Vite proxy, problem-details error mapping, and the expiring sessionStorage session.
6. Re-ran `npm run lint` and `npm run build`. Both pass. No dev server was started.
7. Copied the project to `outputs/app/`, leaving out node_modules, dist and package-lock.json.

## Final `npm run lint`
```
> task-management-ui@0.0.0 lint
> eslint .
(exit code 0, no warnings or errors)
```

## Final `npm run build`
```
> task-management-ui@0.0.0 build
> tsc -b && vite build
vite v8.3.4 building client environment for production...
✓ 106 modules transformed.
dist/index.html                   0.46 kB │ gzip:  0.30 kB
dist/assets/index-CVN15ulJ.css    3.81 kB │ gzip:  1.29 kB
dist/assets/index-j-7tKGpx.js   265.97 kB │ gzip: 84.74 kB
✓ built in 597ms
```

## Message to the user
The task management frontend is set up in `...\scratchpad\apps\disabled-submit-variant-with_skill`. It uses Vite 8, React 19 and TypeScript, with ESLint. `npm run lint` and `npm run build` both pass.

**Login page:**
- The username only accepts an email address. Spaces are removed as you type, and it must look like `name@domain.tld` (at most 256 characters).
- The password must be 1–128 characters. Both limits match the API's `LoginCommandValidator`.
- **Sign in stays disabled until both fields are valid**, and also while the request is being sent. A short hint under the button explains why it's disabled.
- It sends `POST /api/auth/login` to AuthController. A 401 always shows "Invalid username or password.", and API validation errors appear on the matching field.
- After you sign in, the token is kept in sessionStorage until it expires. You land on a placeholder `/tasks` page with a Sign out button.

**To run it:** start TaskManagement.Api with the **https** profile (https://localhost:7213). Then run `npm run dev` and open http://localhost:5173.

**CORS:** the API only allows `https://localhost:5173`, but Vite serves `http://`. In development, Vite forwards `/api` to the API (set by `API_PROXY_TARGET` in `.env.development`), so you don't need to change anything. For a deployed build, set `VITE_API_BASE_URL` and add the UI's address to the API's `Cors:AllowedOrigins`.

**Test users** come from the API's `Auth:Users` setting and only work in Development. Look in the API's user secrets or `appsettings.Development.json`.

**Autofill:** some browsers fill a saved password without telling the page until you interact with it. Sign in can then look disabled until you click or type in a field.
