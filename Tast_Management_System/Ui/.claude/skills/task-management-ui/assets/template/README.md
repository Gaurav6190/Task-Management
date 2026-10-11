# Task Management UI

React 19 + TypeScript + Vite, linted with ESLint. Signs in against **TaskManagement.Api**, then lists,
creates and updates tasks.

## Run it

1. Start the API with its `https` launch profile (`https://localhost:7213`).
2. In this folder:

   ```bash
   npm install
   npm run dev
   ```

3. Open http://localhost:5173. You'll land on `/login`, then `/tasks` once signed in.

| Script            | What it does                         |
| ----------------- | ------------------------------------ |
| `npm run dev`     | Dev server with `/api` proxied       |
| `npm run lint`    | ESLint                               |
| `npm run build`   | Type-check (`tsc -b`) and build      |
| `npm run preview` | Serve the production build           |

## Screens

- **Login (`/login`):** the username field only accepts an email address. Whitespace is stripped as you type, and
  the form won't submit until the value is a valid address (max 256 characters). The password must be 1–128
  characters. These limits match the API's `LoginCommandValidator`.
- **Tasks (`/tasks`, signed in only):**
  - **Table:** Title (and description), Status, Priority, Assigned to, Due date. Done tasks are struck through;
    late ones are marked **Overdue**. Pages of 20, with Previous/Next.
  - **Filters:** the Status and Priority column headers each have a filter. The API filters across all pages.
  - **New task:** a dialog with Title, Description, Priority, Assigned to and Due date, using the API's rules.
  - **Inline edits:** each row's Status and Priority are dropdowns. A change is saved straight to the database
    (`PUT /api/tasks/{id}`), then the row is refreshed from it. A failed save reverts the row and shows why.
  - **Colours:** status To do grey / In progress blue / Done green; priority Low grey / Medium violet /
    High orange / Critical red, with ↓ = ↑ ⚠ icons and a coloured bar on each row. Light theme only.

## How the UI talks to the API

- In development, Vite proxies `/api` to `API_PROXY_TARGET` (see `.env.development`), so the API's
  CORS list doesn't matter. Use `http://localhost:5008` there if you run the API's `http` profile.
- For a deployed build, set `VITE_API_BASE_URL` to the API's URL and add the UI's origin to the API's
  `Cors:AllowedOrigins`.
- The token is kept in `sessionStorage`, and the user is signed out when it expires or the API answers 401.
- `PUT /api/tasks/{id}` replaces the whole task, so updates always send every field
  (`toUpdateRequest` in `src/features/tasks/taskApi.ts`).

## Layout

```
src/
  api/                httpClient.ts (fetch + problem-details errors), authApi.ts
  auth/               AuthProvider, useAuth, RequireAuth, session storage, credential validation
  lib/dates.ts        UTC date helpers (due dates are calendar days in UTC)
  features/tasks/
    taskTypes.ts      types, labels, limits      taskApi.ts   the only code that calls /api/tasks
    useTasks.ts       list state, create, inline update      taskErrors.ts  readable API errors
    display/          TaskTable, TaskRow, ColumnFilter, Pagination, EmptyState
    create/           CreateTaskDialog, CreateTaskForm, validation, API error mapping
    update/           StatusSelect, PrioritySelect, SaveIndicator, useRowUpdate
    priority/         PriorityIcon
  pages/              LoginPage, TasksPage
```
