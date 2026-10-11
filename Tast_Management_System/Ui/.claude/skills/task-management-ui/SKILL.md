---
name: task-management-ui
description: Scaffold and extend the Task Management System's React front end (Vite + React + TypeScript, linted with ESLint, light theme). It builds an email-only login page wired to TaskManagement.Api's POST /api/auth/login, and a task table with Status/Priority column filters, a New task dialog, and inline Status/Priority dropdowns that save to the database through PUT /api/tasks/{id}. Use this whenever the user wants to create, bootstrap, set up, regenerate or change the Task Management UI, its login page, its task list/table, task creation, status or priority updates, priority colours, filters or styling. Also use it when they start a new Vite/React/TS app for the task management project, or ask why the UI can't reach the API (CORS, proxy, ports), even if they don't say "Vite" or name this skill.
---

# Task Management UI

Creates the front end for the Task Management System: a Vite + React 19 + TypeScript app, linted with
ESLint, that talks to **TaskManagement.Api** (`../Api` next to the `Ui` folder). It has two screens:

- **`/login`:** username (email address only) and password.
- **`/tasks`:** a table of tasks with these parts:
  - Status and Priority filters in the column headers,
  - a **New task** dialog,
  - Status and Priority dropdowns on every row that save straight to the database,
  - colour-coded status and priority on a light theme.

Most of the work is done by a tested scaffold script plus template files. Your job is to run it,
confirm it works, and explain the result. Stay out of the template code unless the user asks for
something different. When they do, keep the properties in "Why the template is built this way".

If the project has a `.claude/task.md` work list, it is the source of truth for what has been agreed
(its "Decisions" table) and what is done. Read it before changing the task screens, and tick off items
you complete.

## What gets built

```
<target>/
  .env.development          API_PROXY_TARGET (Vite proxy) + VITE_API_BASE_URL
  vite.config.ts            /api proxy -> https://localhost:7213
  eslint.config.js          create-vite's config, plus ignoring .claude/ (skills and test workspaces)
  src/
    main.tsx, App.tsx       BrowserRouter + AuthProvider; /login, /tasks (protected), * -> /tasks
    index.css               design tokens: light theme only, status + priority colours
    config.ts               API_BASE_URL
    lib/dates.ts            UTC date helpers (parseUtc, due-date input <-> ISO, formatting)
    api/                    httpClient (fetch, ApiError/NetworkError from problem details), authApi
    auth/                   AuthProvider, useAuth, RequireAuth, session (sessionStorage), credentials, loginErrors
    features/tasks/
      taskTypes.ts          TaskDto, TaskStatus/TaskPriority unions, labels, limits
      taskApi.ts            listTasks, getTask, createTask, updateTask, toUpdateRequest, isUnauthorized
      taskErrors.ts         describeTaskError: one readable sentence per failure
      useTasks.ts           list state (page, filters, stale-response guard), createTask, updateTaskFields
      display/              TaskTable, TaskRow, ColumnFilter, Pagination, EmptyState
      create/               CreateTaskDialog (<dialog>), CreateTaskForm, taskFormValidation, createTaskErrors
      update/               StatusSelect, PrioritySelect, rowSelect.module.css, SaveIndicator, useRowUpdate
      priority/             PriorityIcon (↓ = ↑ ⚠)
    pages/                  LoginPage, TasksPage
```

## Workflow

### 1. Pick the target folder

Default to the `Ui` folder of the Task Management System (usually the current working directory,
`...\Tast_Management_System\Ui`; the project folder really is spelled "Tast"). If the user named
another folder or app name, use that. The script works in a folder that already contains `.claude/`,
`.git/`, `.vscode/` and similar, and keeps those files.

### 2. Find Node

Run `node --version`. Vite 8 needs Node 20.19+ or 22.12+. On Windows, Node is often installed at
`C:\Program Files\nodejs\` but missing from the shell's PATH. If `node` isn't found, check that path and
call `node.exe` by its full path. The script then puts Node's folder on PATH for npm/npx itself. If
Node isn't installed anywhere, stop and tell the user to install the current LTS from nodejs.org.

### 3. Run the scaffold script

```
node <this-skill>/scripts/create-app.mjs <target-dir> --name task-management-ui
```

It will:
1. run `create-vite@9 --template react-ts --eslint` in a temp folder,
2. delete the demo files (`App.css`, `src/assets`, `public/icons.svg`, README) and copy `assets/template/` over,
3. set the package name and page title,
4. copy everything into the target. **It refuses to overwrite existing files unless `--force` is passed.**
   If it lists conflicts, show them to the user and ask before re-running with `--force`. They may be
   overwriting real work.
5. `npm install`, `npm install react-router@^8`, then `npm run lint` and `npm run build`.

It takes about a minute, mostly `npm install`; give it a generous timeout (5+ minutes). Use
`--skip-install` only if the user explicitly doesn't want dependencies installed.

### 4. Verify

The script exits non-zero if lint or build fails. If it passed, the app is in a known-good state. If
the user asked for changes on top of the template, re-run `npm run lint` and `npm run build` after
making them. "It compiles and lints clean" is the bar before you call it done. If you can't check it in
a browser or against the running API, say so plainly rather than implying it was tested end to end.

Don't start `npm run dev` unless the user asks. It never exits, so run it in the background if you do.

### 5. Report back

Tell the user, briefly:
- where the app is and that lint + build passed,
- how to run it: start TaskManagement.Api with the **https** profile, then `npm run dev`, then open
  http://localhost:5173,
- the CORS point below, because it will bite them otherwise,
- that test users come from the API's `Auth:Users` config (Development only). Only a password *hash*
  is stored there, so you can't log in yourself unless the user gives you the password. Don't create or
  change users in the API's secrets to get around that.

## Why the template is built this way

Keep these properties when you adapt or extend the app. Each one exists for a reason.

### Login

**The username accepts only an email address.** This is the user's core requirement, so it is
enforced in layers. No single layer is enough:
- `type="email"` + `inputMode="email"` + `autoComplete="username"` give mobile users the email
  keyboard and let password managers fill the field.
- `onChange` strips all whitespace (`sanitizeEmailInput`), so spaces can't be typed or pasted in.
- `validateUsername` checks a `local@domain.tld` pattern and the 256-character limit. Errors show
  after blur or the first submit attempt, not on the first keystroke.
- `noValidate` on the form turns off the browser's inconsistent built-in bubbles, so our messages are
  the only ones shown and they are announced via `aria-describedby`.
- The API's FluentValidation `EmailAddress()` is the last line of defence. Its 400 `errors.Username`
  is mapped back onto the field.

**A 401 at login always says "Invalid username or password."**, for an unknown email or a wrong
password, so the UI never reveals which accounts exist.

**The session lives in sessionStorage and expires.** `expiresAtUtc` drives an automatic sign-out
timer, and any 401 from a task call signs out too (handled once, in `useTasks`). sessionStorage
survives a refresh but not closing the tab, which is a reasonable default for a JWT readable from JS.
Don't switch to localStorage unless the user asks.

### Talking to the API

**API calls go through a Vite proxy.** The API's CORS allows only `https://localhost:5173`, but Vite
serves `http://`. Proxying `/api` to `API_PROXY_TARGET` keeps requests same-origin in development, so
the user doesn't have to touch the API. For a deployed build, set `VITE_API_BASE_URL` and add the UI's
origin to the API's `Cors:AllowedOrigins`.

**Errors come from problem details.** Every API error is `application/problem+json` with a stable
`code` and, on 400, field `errors` keyed C#-style (`Title`, `DueDateUtc`). Map on `code`/status and
field keys, never on English text.

**`taskApi.ts` is the only code that calls `/api/tasks`.** Components and hooks go through it.

### Task table

**A table, with the Status and Priority filters in the column headers.** The user chose a table over
cards. The API does the filtering (`?status=&priority=`), so a filter covers every page, not just the
visible 20. A filter change goes back to page 1. When filters match nothing, the header row stays,
with a "No tasks match these filters" row, so the user can always change the filters back. On narrow
screens the table scrolls sideways inside its own box (`min-width` 920px) and the Title column is sticky.

**Responses are tagged with the page and filters they were for** (`useTasks`), so a slow, outdated
response can't overwrite a newer one. The previous rows stay, dimmed, while the next page loads.

### Creating tasks

**The form mirrors `CreateTaskCommandValidator`:** title required (≤ 200 after trimming), description
≤ 4000, assigned-to ≤ 100, due date not before today. "Today" is the **UTC** day, because that is what
the API checks. Due dates are calendar days sent as `YYYY-MM-DDT00:00:00Z` and shown in UTC, so they
read the same in every time zone. After creating, the filters are cleared and page 1 reloads, so the new
task is visible at the top. A failed request keeps everything the user typed.

### Updating status and priority

**`PUT /api/tasks/{id}` replaces the whole task.** Always build the body with `toUpdateRequest(task,
changes)`, which copies every field. Sending only `{ status }` fails validation; forgetting a field
would wipe it. Send `dueDateUtc` back exactly as received: the API only rejects a past due date when it
*changes*, so overdue tasks stay editable.

**"Saved" means the database has it.** A row change shows straight away, but it is marked saving until
the API answers 204. The row is then replaced with `GET /api/tasks/{id}`, so it shows what SQL Server
holds (`ModifiedDate`, `CompletedAtUtc`). On failure the row reverts and the cell says why: 404 removes
the row with a notice, 409 reloads the list.

**One save per row at a time.** Both dropdowns in a row are disabled while it saves, because two
overlapping full-object PUTs could undo each other. Other rows stay editable. A row that no longer
matches the active filter after an edit stays visible, with a note, until the next reload, so it doesn't
vanish under the cursor.

### Look

**Light theme only.** The user asked for it, so there is no `prefers-color-scheme: dark` block and
`color-scheme` is `light`. Don't add dark mode back unless asked. The status and priority colours were
chosen for light backgrounds.

**Status and priority are colour-coded, and colour is never the only signal.**

| Status | Colour | | Priority | Colour | Icon |
|---|---|---|---|---|---|
| To do | grey | | Low | grey | ↓ |
| In progress | blue | | Medium | violet | = |
| Done | green | | High | orange | ↑ |
| | | | Critical | red, plus a row tint | ⚠ |

- Each value has `-fg` / `-bg` / `-border` tokens in `index.css`, and every `-fg` meets WCAG AA (≥ 4.5:1)
  on its `-bg` and on white. Re-check contrast if you change a colour.
- The dropdowns are tinted through `data-status` / `data-priority` in `update/rowSelect.module.css`.
- Each row's priority bar is an inset shadow on the sticky Title cell, keyed on `data-priority` on the
  `<tr>`.
- Medium is violet, not blue, so it can't be confused with In progress.

**No CSS framework.** Use CSS Modules and the tokens in `index.css`; don't hard-code colours in
components.

### Code shape

**One folder per job, one job per file:** `display/`, `create/`, `update/` and `priority/` under
`features/tasks/`. The user asked for this split; don't merge features back into one big file.

**The files are also split to satisfy the generated ESLint config.** `eslint-plugin-react-refresh`
flags files that export both components and non-components. That is why the context object
(`AuthContext.ts`), the provider (`AuthProvider.tsx`) and the hook (`useAuth.ts`) are separate files,
and why helpers live in `.ts` files apart from components.

The tsconfig has `verbatimModuleSyntax` (use `import type`) and `erasableSyntaxOnly` (no `enum`; use
string-literal unions such as `TASK_STATUSES`). `eslint-plugin-react-hooks` v7 rejects synchronous
`setState` inside effects; set state in async callbacks, or derive it, as `useTasks` does with its
`loading` flag. Follow the same rules in any code you add, or lint or build fails.

## If the script can't be used

If the script fails for an environmental reason you can't fix (offline npm, a locked file), do it by
hand. Watch for the traps the script handles for you:
1. `npx create-vite@9 <dir> --template react-ts --eslint --no-interactive`. **Without `--eslint`,
   create-vite 9 sets up Oxlint instead**, and the user asked for ESLint.
2. Never run create-vite with `--overwrite` in a folder that has other content: it deletes
   everything, including `.claude/`. Scaffold into an empty folder and copy the files across.
3. Copy `assets/template/` over the scaffold, delete `src/App.css` and `src/assets/`, then
   `npm install && npm install react-router@^8 && npm run lint && npm run build`.

## Changing or extending it

- **API contract details** (endpoints, fields, limits, error codes, ports, CORS): read
  `references/api-contract.md`. If the user says the API changed, re-read the C# files it points to
  rather than trusting the doc.
- **New task-related calls** go in `taskApi.ts`, using `apiRequest(path, { token })`. Route errors through
  `describeTaskError`, and let `useTasks` handle 401.
- **Editing more fields inline:** extend `TaskFieldChanges` and add a select or input beside the existing
  ones. It saves through the same `updateTaskFields` path, so it inherits revert, refresh and row locking.
- **Sorting:** the API has no sort parameter and always returns newest first. Add one to the API before
  offering column sorting; sorting only the visible page would be misleading.
- **Styling** uses plain CSS Modules with tokens in `src/index.css`. If the user wants Tailwind or a
  component library, add it after scaffolding and keep the behaviour described above.
