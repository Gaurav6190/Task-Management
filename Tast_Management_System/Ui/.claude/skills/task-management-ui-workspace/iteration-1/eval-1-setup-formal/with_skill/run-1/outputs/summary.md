# setup-formal / with_skill

## Steps
1. Read the skill's SKILL.md, scripts/create-app.mjs and references/api-contract.md.
2. Checked the API (read-only) against the reference doc:
   - `AuthController` has `[Route("api/auth")]` and `[HttpPost("login")]`.
   - The https launch profile is `https://localhost:7213;http://localhost:5008`.
   - `Cors:AllowedOrigins` is `["https://localhost:5173"]`.

   All of these match the doc, so the template needed no changes.
3. `node` was not on PATH. Found Node v24.21.0 at `C:\Program Files\nodejs\node.exe` and called it by its full path.
4. Ran `node .claude/skills/task-management-ui/scripts/create-app.mjs <target> --name task-management-ui`. The script:
   - ran `npx create-vite@9 app --template react-ts --eslint --no-interactive` in a temp folder,
   - removed the demo files and applied the template,
   - copied the result into the target with no conflicts,
   - ran `npm install` and `npm install react-router@^8`,
   - ran `npm run lint` and `npm run build`. The script exited with 0.
5. Spot checks:
   - `eslint.config.js` is present, so the linter is ESLint and not Oxlint.
   - `LoginPage.tsx` has `type="email"`, `autoComplete="username"`, `sanitizeEmailInput` (strips whitespace) and `noValidate` with custom email validation.
   - `vite.config.ts` proxies `/api` to `https://localhost:7213`.
6. Did not start a dev server. Copied the project to outputs/app/ without node_modules, dist and package-lock.json.

## npm run lint
```
> task-management-ui@0.0.0 lint
> eslint .
```
No warnings or errors.

## npm run build
```
> task-management-ui@0.0.0 build
> tsc -b && vite build

vite v8.3.4 building client environment for production...
✓ 106 modules transformed.
dist/index.html                   0.46 kB │ gzip:  0.30 kB
dist/assets/index-BbVG_ULK.css    3.64 kB │ gzip:  1.24 kB
dist/assets/index-DvzF1HaZ.js   265.69 kB │ gzip: 84.64 kB
✓ built in 6.20s
```

## Message to the user
The Task Management UI is set up in `<target folder>`. It uses Vite, React 19 and TypeScript, linted with ESLint (typescript-eslint, react-hooks, react-refresh). `npm run lint` is clean and `npm run build` passes.

**What's there**
- **`/login`:** a username and password form. The username field only accepts an email address:
  - it is an email input,
  - spaces are removed as you type or paste,
  - it is checked against the `name@domain.tld` pattern and the API's 256-character limit,
  - an invalid value blocks the request and shows an error under the field.

  The API's own validation errors are also shown on the matching fields.
- **Login request:** it calls `POST /api/auth/login` on TaskManagement.Api (`AuthController`). The returned JWT is kept in sessionStorage, and you are signed out automatically when it expires. A wrong email and a wrong password both show "Invalid username or password.", so the page doesn't reveal which accounts exist.
- **`/tasks`:** a placeholder page behind login, with a Sign out button.

**To run it**
1. Start TaskManagement.Api with the **https** profile (https://localhost:7213).
2. In the app folder, run `npm run dev` and open http://localhost:5173.

**CORS:** the API only allows `https://localhost:5173`, but Vite serves plain http. To get around this, the dev server proxies `/api` to the API (`API_PROXY_TARGET` in `.env.development`), so you don't need to change the API for local development. For a deployed build, set `VITE_API_BASE_URL` and add the UI's origin to the API's `Cors:AllowedOrigins`.

**Logging in:** test users come from the API's `Auth:Users` configuration, which only works in the Development environment. They are in the API's user secrets or `appsettings.Development.json`. The UI contains no hard-coded credentials.
