# Summary: casual-with-tasks-page (with skill)

## Steps
1. Read the skill's `SKILL.md`, `references/api-contract.md` and `scripts/create-app.mjs`.
2. `node` was not on PATH. Found Node v24.21.0 at `C:\Program Files\nodejs\node.exe` and called it by full path.
3. Ran the scaffold script into the new, empty target folder: `node .claude/skills/task-management-ui/scripts/create-app.mjs <target> --name task-management-ui`. It ran `create-vite@9 --template react-ts --eslint`, applied the template, then `npm install`, `npm install react-router@^8`, `npm run lint` and `npm run build`. Exit code 0.
4. Checked the template against the request. No changes were needed:
   - The login page only accepts an email as the username: email input, whitespace stripped, format and 256-character checks, and the API's 400 errors are shown on the field.
   - It calls `POST /api/auth/login`.
   - After login, `/tasks` is an empty page with a "Sign out" (logout) button.
5. Checked the API (read-only):
   - `AuthController` is `[Route("api/auth")]` with `[HttpPost("login")]`.
   - The https profile is `https://localhost:7213`.
   - CORS allows only `https://localhost:5173`.

   This matches the contract doc and the template's Vite proxy.
6. Copied the project to `outputs/app/` without `node_modules`, `dist` and `package-lock.json`. No dev server was started.

## `npm run lint`
```
> task-management-ui@0.0.0 lint
> eslint .
```
(no warnings or errors)

## `npm run build`
```
> task-management-ui@0.0.0 build
> tsc -b && vite build

vite v8.3.4 building client environment for production...
✓ 106 modules transformed.
dist/index.html                   0.46 kB │ gzip:  0.30 kB
dist/assets/index-BbVG_ULK.css    3.64 kB │ gzip:  1.24 kB
dist/assets/index-DvzF1HaZ.js   265.69 kB │ gzip: 84.64 kB
✓ built in 5.95s
```

## Message to the user
Your React frontend is ready at `<target folder>`: Vite + React + TypeScript with ESLint. `npm run lint` is clean and `npm run build` passes.

**What it does**
- **Login (`/login`)** is the first screen. The username field only accepts an email address: it uses an email input, strips any spaces typed or pasted in, and checks the format and the 256-character limit before anything is sent. It signs in with `POST /api/auth/login`. Wrong credentials show "Invalid username or password." API validation errors appear on the matching field.
- **Tasks (`/tasks`)** requires login. It's an empty page with your email and a **Sign out** button. The session is kept in sessionStorage and signs you out automatically when the token expires.

**To run it**
1. Start TaskManagement.Api with the **https** profile (`https://localhost:7213`).
2. Run `npm run dev` in the app folder and open http://localhost:5173.

**Heads-up on CORS:** the API allows only `https://localhost:5173`, but Vite serves plain `http`. To get around this, the dev server proxies `/api` to the API (see `vite.config.ts` / `.env.development`), so you don't need to change the API for local development. For a deployed build, set `VITE_API_BASE_URL` and add the UI's origin to the API's `Cors:AllowedOrigins`.

**Logging in:** test users come from the API's `Auth:Users` configuration, which works in Development only. Check the API's user secrets or `appsettings.Development.json`.
