# Task Management UI: Work Items

How to use this file: there is one section per work item, with a checkbox for each sub-task. Tick items as you finish them, and add new work items at the bottom with the next ID.

The UI is a Vite + React 19 + TypeScript app linted with ESLint. It was scaffolded by the project skill `.claude/skills/task-management-ui`. It talks to **TaskManagement.Api** (`../Api`). Every `/api/tasks` call needs `Authorization: Bearer <token>` from the login.

**Status legend:** `[ ]` not started · `[~]` in progress · `[x]` done

---

## Decisions

| # | Topic | Decision |
|---|---|---|
| D1 | App shape | Single-page app. `/login` (done) and `/tasks` (the task list). React Router handles navigation, with no full page reloads. |
| D2 | Layout | **Table layout** (changed from cards on 2026-10-11). One row per task, with these columns: **Title**, **Status**, **Priority**, **Assigned to**, **Due date**. A table is easier to scan and compare across many tasks, and gives filters a natural home in the column headers (D9). |
| D3 | Inline status and priority update | Each row has a **dropdown** (`<select>`) in its **Status** cell (To do, In progress, Done) and in its **Priority** cell (Low, Medium, High, Critical). Changing either one saves straight away to the database through `PUT /api/tasks/{id}`; there is no separate Save button. Dropdowns work with the keyboard and screen readers, and need no drag-and-drop library. Drag-and-drop is a possible later work item (T9). Added priority on 2026-10-11. |
| D4 | Priority display | A coloured 4px bar on the row's left edge, plus a badge with an icon and the priority's name in the Priority cell. Colour is never the only signal. |
| D5 | Styling | Plain CSS Modules with the design tokens in `src/index.css`. **Light theme only** (changed 2026-10-11): the app no longer follows the operating system's dark mode, so the status and priority colours always sit on the light backgrounds they were chosen for. No CSS framework. |
| D12 | Status colours | Status dropdowns are tinted by value: **To do** grey, **In progress** blue, **Done** green. Priority uses its own scale: **Low** grey, **Medium** violet, **High** orange, **Critical** red. Every text colour meets WCAG AA (≥ 4.5:1) on its tint and on white; the lowest is 5.39. |
| D6 | Code structure | One folder per feature, and inside it one folder per job: **display**, **create**, **update**, **priority**. No component file holds more than one job (see "Folder layout"). |
| D7 | Data loading | A small `useTasks` hook (fetch, state and refresh). No extra data library for now. |
| D8 | Full-object PUT | `PUT /api/tasks/{id}` replaces the whole task, so a status change sends **all** fields: title, description, status, priority, assignedTo and dueDateUtc. Send `dueDateUtc` back exactly as received. The API only rejects a past due date when it changes. |
| D9 | Filtering | The **Status** and **Priority** column headers each have a filter dropdown ("All" plus each value). The API does the filtering (`?status=&priority=`) across all pages, not just the visible one. Changing a filter goes back to page 1. The header row stays visible when no rows match, so the filters can always be changed back. |
| D10 | Small screens | Below about 920px (raised from 760px in T4, when the Status and Priority cells became dropdowns) the table scrolls sideways **inside its own container**, and the Title column stays fixed (sticky) on the left. The page itself never scrolls sideways. |
| D11 | Saved means saved | A row change only counts as done once the API answers **204** (EF Core has committed it to SQL Server). Then the row is refreshed from `GET /api/tasks/{id}`, so it shows what the database holds, including the new `modifiedDate` and `completedAtUtc`. Until then the change is shown as pending, and on failure the row goes back to its saved values. |

---

## Folder layout (target)

```
src/
  features/tasks/
    taskTypes.ts              TaskDto, TaskStatus, TaskPriority, PagedResult, labels, field limits
    taskApi.ts                listTasks(), getTask(), createTask(), updateTask(): API calls only
    useTasks.ts               load, reload, filters, paging, create, updateTaskFields(); holds list state
    taskErrors.ts             describeTaskError(): one readable message per API failure
    display/
      TaskTable.tsx           <table> shell: header row with column filters, body states (loading / empty / error)
      TaskTable.module.css
      TaskRow.tsx             one <tr>: title + description, status, priority, assignee, due date
      TaskRow.module.css
      ColumnFilter.tsx        filter <select> shown inside a column header (Status, Priority)
      ColumnFilter.module.css
      EmptyState.tsx          "No tasks yet" + call to create one
      Pagination.tsx          Previous / Next, "Showing 1–20 of 57", "Page X of Y"
    create/
      CreateTaskDialog.tsx    modal wrapper (open/close, focus handling)
      CreateTaskDialog.module.css
      CreateTaskForm.tsx      the form fields + submit
      CreateTaskForm.module.css
      taskFormValidation.ts   client rules that mirror the API validators; form values -> request
      createTaskErrors.ts     API 400 field errors -> form fields
    update/
      StatusSelect.tsx        status dropdown in the Status cell of TaskRow
      PrioritySelect.tsx      priority dropdown (with the priority icon/colour) in the Priority cell of TaskRow
      rowSelect.module.css    shared styles for both row dropdowns
      SaveIndicator.tsx       spinner while saving, tick when saved (+ screen-reader announcement)
      SaveIndicator.module.css
      useRowUpdate.ts         per-row save state: pending field, error message, revert
    priority/
      PriorityIcon.tsx        ↓ = ↑ ⚠ icon per priority, in the priority colour
      PriorityIcon.module.css
      PriorityBadge.tsx       read-only icon + label (only when a read-only view needs it)
  pages/
    TasksPage.tsx             puts it together: header, toolbar, TaskTable, Pagination, CreateTaskDialog
    TasksPage.module.css
```

The card version's `display/TaskCard.*`, `display/TaskList.*` and `display/TaskFilters.*` were deleted in T2. The filters now live in the column headers.

---

## API contract the UI depends on

| Operation | Method & route | Success | Errors |
|---|---|---|---|
| List | `GET /api/tasks?status=&priority=&page=1&pageSize=20` | 200 `PagedResult<TaskDto>` | 400, 401 |
| Get one | `GET /api/tasks/{id}` | 200 `TaskDto` | 401, 404 |
| Create | `POST /api/tasks` | 201 `{ id }` | 400, 401 |
| Update | `PUT /api/tasks/{id}` | 204 | 400, 401, 404, 409 |

- **TaskDto:** `id`, `title`, `description?`, `status`, `priority`, `assignedTo?`, `dueDateUtc?`, `createdDate`, `modifiedDate?`, `completedAtUtc?`
- **PagedResult:** `items`, `page`, `pageSize`, `totalCount`, `totalPages`. `pageSize` is at most 100. Results come newest first.
- **Status** values are `ToDo`, `InProgress` and `Done`. **Priority** values are `Low`, `Medium`, `High` and `Critical`. Both are sent as names, not numbers.
- **Validation:**
  - `title` is required, at most 200 characters.
  - `description` is at most 4000 characters.
  - `assignedTo` is at most 100 characters.
  - `priority` is required.
  - On create, `dueDateUtc` can't be in the past.
- **Errors** are `application/problem+json` with a `code` field. 400 responses also have `errors` with keys such as `Title` and `Priority`. `src/api/httpClient.ts` already turns these into an `ApiError`.

---

## T1: Task types and API client [x]

**Goal:** one typed place that talks to `/api/tasks`, so the components never call `fetch` themselves.

- [x] `taskTypes.ts`:
  - `TaskStatus` and `TaskPriority` as string-literal unions, with arrays such as `TASK_STATUSES` and `TASK_PRIORITIES`. No `enum`: the tsconfig sets `erasableSyntaxOnly`.
  - The `TaskDto` and `PagedResult<T>` interfaces.
  - Display labels (`ToDo` → "To do", `InProgress` → "In progress").
  - Field limits (200, 4000 and 100).
- [x] `taskApi.ts`:
  - `listTasks({ page, pageSize, status?, priority? }, token)`
  - `createTask(body, token)`, which returns the new `id`
  - `updateTask(id, body, token)`

  All three use `apiRequest` from `src/api/httpClient.ts` with `{ token }`.
- [x] `updateTask` takes a full `UpdateTaskRequest` (D8). Add a helper `toUpdateRequest(task, changes)` that copies every field from the `TaskDto` and applies the changes.
- [x] Dates: send `dueDateUtc` as ISO UTC (`YYYY-MM-DDT00:00:00Z`). When reading, treat a value without `Z` as UTC.
  - Done in the shared `src/lib/dates.ts`. `parseUtc` moved there from `auth/session.ts`, which now imports it.
  - Helpers: `dateInputToUtcIso`, `utcIsoToDateInput`, `todayUtcDateInput` (the `min` for the due-date field), `formatUtcDate`, `formatLocalDate` and `isPastUtcDate`.
  - Due dates are calendar days, so they are read and shown in UTC to keep the same day in every time zone. The earliest date offered is today in UTC, because that is what the API's "not in the past" check uses.
- [x] On any `ApiError` with status **401**, call `logout()` so the user goes back to `/login`. Put this in one place, inside `useTasks`, not in every component.
  - The `isUnauthorized(error)` helper is in `taskApi.ts`, and `useTasks` (T2) calls `logout()` when it returns true.

## T2: Display tasks in a table with column filters [~]

**Goal:** the requirement *"displays tasks in a table or card layout"*, as a table (D2) with Status and Priority filters in the column headers (D9). Code goes in `features/tasks/display/`.

A card version was built first. The data layer, pagination and empty state carry over unchanged. The card components are replaced by the table.

**Kept from the card version**
- [x] `useTasks.ts` loads page 1 (`pageSize` 20) after login. It exposes `tasks`, `loading`, `error`, `page`, `totalPages`, `totalCount`, `hasLoaded`, `filters`, `setFilters()`, `reload()` and `setPage()`.
  - Each response is tagged with the page and filters it was for, so a slow, outdated response can't replace a newer one.
  - `setFilters()` goes back to page 1.
  - A 401 calls `logout()`.
- [x] `taskErrors.ts` has `describeTaskError(error)`: one readable message for any API failure, reused by T3 and T4.
- [x] `Pagination.tsx`: Previous/Next and "Page X of Y" when `totalPages > 1`.
- [x] `EmptyState.tsx`: "No tasks yet", with a slot for the New task button.
- [x] `TasksPage.tsx` header: sticky, with the app name, the user's email and Sign out.

**Table**
- [x] `TaskTable.tsx` renders a real `<table>`:
  - a visually hidden `<caption>` ("Tasks"),
  - `<th scope="col">` headers,
  - a `<tbody>` with one `TaskRow` per task.
- [x] Columns, in order:

  | Column | Content | Width |
  |---|---|---|
  | Title | Title (bold, 1 line, ellipsis; full text in a tooltip) with the description beneath it (muted, 1 line, ellipsis, hidden if empty) | flexible, takes the remaining space |
  | Status | `StatusSelect` (T4). Until T4 is done, a read-only status chip | ~150px |
  | Priority | `PriorityIcon` + `PrioritySelect` (T4, T5) | ~170px |
  | Assigned to | Name, or "Unassigned" (muted) | ~160px |
  | Due date | Formatted in UTC (`formatUtcDate`), with an **Overdue** chip when the date has passed and the task isn't Done. "—" when there is no due date | ~140px |

- [x] `TaskRow.tsx` styles:
  - Done rows are muted, with the title struck through. The Due date cell shows "Completed {date}" (`formatLocalDate(completedAtUtc)`) instead of the due date.
  - Rows are highlighted on hover.
  - Cells are vertically centred, with 12px vertical and 16px horizontal padding.
  - Numbers and dates use `font-variant-numeric: tabular-nums`.
  - The title cell is a `<th scope="row">`, so screen readers announce each row by its task title.
  - Every cell in a row reads its background from one `--row-bg` variable. That keeps the sticky Title cell the same colour as its row when hovered.
- [x] Loading:
  - On first load, show 5 skeleton rows under the real header, so the layout doesn't jump.
  - While the next page or filter loads, keep the old rows and dim the `<tbody>` (`aria-busy` on the table).
- [x] An error banner with a **Retry** button above the table. The table and its filters stay usable. If the very first load fails, only the banner is shown.

**Column filters** (D9)
- [x] `ColumnFilter.tsx` is a compact `<select>` placed in a header cell, under the column name:
  - **Status:** All, To do, In progress, Done.
  - **Priority:** All, Low, Medium, High, Critical.
  - Its accessible name is "Filter by status" or "Filter by priority".
  - It is generic over the option type, so one component serves both columns.
- [x] A filter that isn't "All" is visibly active: an accent border, accent text and bold text, rather than a filled background. The toolbar shows **Clear filters** while any filter is active, and the count reads "N tasks matching filters".
- [x] Changing a filter calls `useTasks.setFilters()`. The API does the filtering across all pages, and the list goes back to page 1.
- [x] When filters match nothing, keep the header row and its filters and show a single full-width row: "No tasks match these filters" with a **Clear filters** button.
- [x] When there are no tasks at all and no filters, show `EmptyState` instead of the table. `EmptyState` now only handles this case.
- [ ] Optional: keep the filters in the URL query string (`/tasks?status=Done&priority=High`), so a refresh or a shared link keeps them. Not done yet.

**Page and responsiveness**
- [x] `TasksPage.tsx` toolbar: the "Tasks" heading, the task count and **Clear filters** (when active).
- [ ] The **New task** button joins the toolbar in T3.
- [x] `Pagination.tsx` also shows the range: "Showing 21–40 of 57". The range shows even when there is only one page; Previous/Next only appear when there are more.
- [x] Small screens (D10):
  - The table sits in a wrapper with `overflow-x: auto` and a `min-width` of 920px (originally 760px; raised in T4 for the dropdown columns).
  - The Title column (header and cells) is `position: sticky; left: 0`, with a solid background.
- [ ] Check at 360px that the page doesn't scroll sideways, only the table does. This needs a browser.
- [x] Delete `TaskCard.*`, `TaskList.*` and `TaskFilters.*` from the card version.
- [x] `npm run lint` and `npm run build` pass.
- [ ] Check the result in a browser against the running API.

## T3: Create a task via a form [~]

**Goal:** the requirement *"create a new task via a form"*. Code goes in `features/tasks/create/`.

- [x] `CreateTaskDialog.tsx`:
  - opens from the **New task** button in the toolbar, and uses a native `<dialog>`, which handles focus trapping and the Esc key,
  - returns focus to the button when closed,
  - has a title ("New task") and Cancel and Create buttons.
  - It also has a ✕ close button. Esc goes through React state, so the dialog and `open` never disagree.
  - The **New task** button also appears in the "No tasks yet" empty state.
- [x] `CreateTaskForm.tsx` fields:
  - **Title** (required, `maxLength` 200)
  - **Description** (textarea, `maxLength` 4000, with a character counter)
  - **Priority** (select, default `Medium`)
  - **Assigned to** (optional, `maxLength` 100)
  - **Due date** (`<input type="date">` with `min` set to today in UTC, optional)
- [x] `taskFormValidation.ts`: rules that mirror the API's `CreateTaskCommandValidator`:
  - title required after trimming, at most 200 characters
  - description at most 4000
  - assignedTo at most 100
  - due date not before today

  Show errors after a field loses focus or after a submit attempt, the same way the login page does. A failed submit focuses the first invalid field. `toCreateRequest()` trims every value and sends empty optional fields as `null`.
- [x] On submit:
  - disable the form and show "Creating…",
  - call `createTask` (`useTasks.createTask`, which signs the user out on 401),
  - on 201, close the dialog, reset the form and reload the list so the new task appears at the top of the table. The form only exists while the dialog is open, so it starts empty every time.
- [x] If filters are active and would hide the new task, clear them after creating, so the user sees it. Simplest reliable rule: creating always clears the filters and goes to page 1.
- [x] Map a 400 `errors.Title`, `errors.Priority` and the other field keys onto the matching fields. Show any other error above the form. This is `create/createTaskErrors.ts`; `DueDateUtc` maps to the Due date field.
- [x] Don't lose what the user typed if the request fails.
- [ ] Check in a browser against the running API: create a task, see it at the top of the table, reload the page, and check it is still there.

## T4: Update status and priority on each row, saved to the database [~]

**Goal:** the requirement *"update the status of a task (dropdown or drag)"*, extended (2026-10-11) so each row's **priority** can be changed the same way. Every change is saved to SQL Server through the API (D3, D11). Code goes in `features/tasks/update/`.

**How a change reaches the database:** row dropdown → `useTasks.updateTaskFields()` → `PUT /api/tasks/{id}` with the full task (D8) → `UpdateTaskCommandHandler` → `TaskItem.Update()` → EF Core `SaveChangesAsync()` → the `Tasks` table in `TaskManagementDb`. The API sets `ModifiedDate`, and it sets or clears `CompletedAtUtc` when the status moves to or from Done. Nothing is stored only in the browser.

**API client**
- [x] Add `getTask(id, token)` to `taskApi.ts` (`GET /api/tasks/{id}`). It is used to refresh a row from the database after a save.

**Dropdowns in the row**
- [x] `StatusSelect.tsx`:
  - a `<select>` (To do / In progress / Done) in each row's Status cell,
  - its accessible name is "Status for {title}"; there is no visible label inside the cell, because the column header names it.
- [~] `PrioritySelect.tsx`:
  - [x] a `<select>` (Low / Medium / High / Critical) in each row's Priority cell, replacing the read-only text,
  - [x] its accessible name is "Priority for {title}",
  - [x] it shows the priority icon next to it and is tinted in the priority colour, so the colour coding still shows while it is editable. A native `<select>` can't contain icons, so `PriorityIcon` sits beside it. The tint comes from `data-priority` in `update/rowSelect.module.css`; `StatusSelect` is tinted the same way through `data-status` (D12).
- [x] Both dropdowns match the column filters' look (`ColumnFilter`), but are visibly distinct from them: filters live in the header, editors live in rows. Editors have a lighter border and no accent colour; both share `update/rowSelect.module.css`.

**Saving** (one shared path for both fields)
- [x] `useTasks.updateTaskFields(task, changes)`, where `changes` is `{ status }` or `{ priority }`:
  1. updates the row straight away (optimistic update) and marks that field as **saving**,
  2. calls `updateTask(id, toUpdateRequest(task, changes))`. The full task is sent, so changing the priority never resets the status or anything else (D8),
  3. on **204**, calls `getTask(id)` and replaces the row with the database's copy (D11). If that GET fails, keep the optimistic row; the save itself already succeeded,
  4. on failure, **reverts** the row to its last saved values and shows the error under that row's dropdown.
- [x] `useRowUpdate.ts` keeps the per-row state: which field is saving, and the last error. That keeps `TaskRow` free of request logic.
- [x] While a row is saving, disable **both** of its dropdowns. Each PUT sends the whole task, so two overlapping PUTs for the same row could undo each other. Other rows stay editable.
- [x] Show a small "Saving…" indicator in the cell being saved, then "Saved" briefly (with `aria-live="polite"`), so the user knows it reached the database. This is `update/SaveIndicator.tsx`: a spinner, then a green tick for 2 seconds. Screen readers hear "Saving status…" and then "Status saved".
- [x] Changing a dropdown back to the value it already has does nothing (no request).

**Filters**
- [x] If a Status or Priority filter is active and the row no longer matches it after the change, keep the row visible (marked "Hidden by filter on refresh") until the next reload or page change, so it doesn't vanish under the user's cursor. The note reads "Hidden by the current filter after refresh" and appears under the title.

**Errors**
- [x] **404**: the task was deleted elsewhere. Remove the row and show a short notice. The notice is a dismissible banner above the table (`useTasks.notice`).
- [x] **409**: a concurrency conflict. Reload the list and show "This task was changed by someone else. Please try again." The banner shows this message, and the cell shows "Not saved: changed by someone else."
- [x] **400**: revert and show the API's message. This can happen if the task's stored data no longer passes validation, e.g. a title over 200 characters.
- [x] **401**: sign out (already handled centrally in `useTasks`).
- [x] Network error or API down: revert and show "Cannot reach the Task Management API…" (`describeTaskError`).

**Row appearance after a save**
- [x] After a change to or from Done, the row's look (T2) updates from the refreshed data: struck-through title, and "Completed {date}" from `completedAtUtc`. In Done rows only the read-only text is muted; the dropdowns keep full contrast so they still look editable.
- [x] After a priority change, the row's priority bar and tint (T5) update straight away. They are driven by `data-priority` on the `<tr>`, which changes with the optimistic update.

**Verify it really is in the database**
- [ ] With the API running against LocalDB:
  - change a row's status, then its priority,
  - **reload the page** and check that both changes are still there (they come from `GET /api/tasks`, so they must be in the database),
  - check the row in SQL: `SELECT Title, Status, Priority, ModifiedDate, CompletedAtUtc FROM Tasks WHERE Id = '<id>'`. Status and Priority are stored as text (`InProgress`, `High`, …) by `TaskItemConfiguration`, so they should read exactly as chosen in the UI. `ModifiedDate` should be the time of the change. `CompletedAtUtc` should be set only when the status is Done.
- [ ] Turn the API off, change a row, and check that it reverts and shows the error. Nothing should look saved when it wasn't.

## T5: Priority indication [~]

**Goal:** the requirement *"basic visual indication of priority (colour-coding or icons)"*. Code goes in `features/tasks/priority/`.

- [x] Add colour tokens to `src/index.css` (light theme only, D5), with readable contrast (WCAG AA for the badge text):

  | Priority | Colour | Icon |
  |---|---|---|
  | Low | slate / grey | ↓ arrow down |
  | Medium | violet (blue is already used for the In progress status) | = equals |
  | High | orange | ↑ arrow up |
  | Critical | red | ⚠ alert triangle |

  Each priority has a `-fg`, `-bg` and `-border` token, and the statuses have matching sets (D12). Contrast was checked with a script: every pair passes AA.
- [x] One place for the colours and icons. Instead of a `priorityStyles.ts` lookup, the colours live in CSS, keyed on `data-priority` (and `data-status`). The icons live in `PriorityIcon.tsx`. The look can then change without touching component code.
- [x] `PriorityIcon.tsx`: a different shape per priority (↓ = ↑ ⚠), drawn in the priority colour, and `aria-hidden`. The text label next to it carries the meaning, so colour is never the only signal.
- [ ] `PriorityBadge.tsx` (read-only icon + label): not needed yet, because the table's Priority cell is the editable `PrioritySelect` with `PriorityIcon` beside it. Build it when a read-only view needs one.
- [x] Each `TaskRow` has a 4px bar in the priority colour on its left edge, drawn as an inset `box-shadow` on the sticky Title cell, so it stays visible while the table scrolls sideways.
- [x] Critical rows also get a subtle tinted background, so they stand out when scanning the table. The tint also applies on hover.
- [ ] The options in the Priority column filter show the same icons or colours, so the filter and the badges match. Native `<option>` elements can't show icons and only partly support colour, so this needs a custom dropdown. It is left open.
- [ ] Check in a browser: the colours, the icons, the Critical tint, and the bar staying visible while scrolling sideways.

## T6: Look and feel (clean and professional, no framework) [ ]

**Goal:** requirement (e): no CSS framework, but clean and professional.

- [ ] Use only the existing tokens (`--surface`, `--border`, `--text-muted`, `--accent` and the rest) plus the priority tokens from T5. No hard-coded colours in components.
- [ ] Table styling:
  - The table sits on a `--surface` panel with a 12px radius and the card shadow.
  - The header row has a subtle background (`--chip-bg`) and small, semi-bold, muted header labels.
  - Rows are separated by 1px `--border` lines. No zebra stripes, which would compete with the priority colours.
- [ ] Use consistent spacing (a 4px or 8px scale), with an 8px radius for controls.
- [ ] Every button, select and input has a visible focus outline (`:focus-visible`), as on the login page. This includes the filters and the status dropdowns inside the table.
- [ ] Hover states on rows and buttons. Respect `prefers-reduced-motion` for any transitions.
- [ ] Check at 360px, 768px and 1280px wide: the page never scrolls sideways (only the table container does, D10), and the toolbar wraps neatly.
- [x] Light theme only (D5): the dark-mode rules were removed from `index.css` and `color-scheme` is `light`, so native controls (date picker, dropdowns, scrollbars) are light too.
- [ ] Check in a browser with the operating system set to dark mode: the app should still be light.

## T7: Quality checks [ ]

- [ ] `npm run lint` and `npm run build` (`tsc -b` + `vite build`) both pass with no errors.
- [ ] Add **Vitest** and **React Testing Library**, and test:
  - [ ] the `taskFormValidation` rules,
  - [ ] the `toUpdateRequest` helper (sends every field, keeps `dueDateUtc` unchanged),
  - [ ] that `StatusSelect` reverts after a failed update (mock `taskApi`),
  - [ ] that changing `PrioritySelect` sends a PUT with the new priority **and the unchanged status, title and due date**,
  - [ ] that after a 204 the row is replaced with the `getTask` result,
  - [ ] that both dropdowns in a row are disabled while that row is saving, and other rows are not,
  - [ ] that `PrioritySelect` / `StatusSelect` set `data-priority` / `data-status` to the current value (which is what drives the colours),
  - [ ] that changing a `ColumnFilter` calls `listTasks` with the right `status` / `priority` and page 1,
  - [ ] that the "no matches" row keeps the header filters visible.
- [ ] Manual smoke test against the running API (https profile):
  - log in,
  - see the seeded tasks in the table,
  - filter by Status, then by Priority, then both, then clear,
  - create a task,
  - move it To do → In progress → Done,
  - change its priority Low → Critical,
  - reload the page and check that both changes are still there (they are in the database),
  - check the priority colours,
  - check that sign out and an expired token both return to `/login`.
- [ ] Update `README.md` with the new screens and the folder layout.

## T8: Update the project skill [x]

- [x] Add the tasks feature to `.claude/skills/task-management-ui`:
  - the folder layout above,
  - the table layout and column filters (D2, D9, D10),
  - the full-object PUT rule (D8),
  - inline status and priority editing, saved to the database and refreshed from it (D3, D11),
  - the priority colour mapping.

  This way, a regenerated UI includes the task table.
  - `assets/template/src/` was mirrored from the project's `src/` (47 files), including the light-only `index.css` (D5, D12).
  - The template `README.md` now describes the task screens.
  - `SKILL.md` has a broader description, so it triggers for task-list work as well as login, and a "Why the template is built this way" section for tasks:
    - the table and server-side filters,
    - the stale-response guard,
    - the create form's rules (including UTC due dates),
    - the full-object PUT, "saved means the database has it", and one save per row at a time,
    - light theme only, and the colour table with its contrast rule,
    - the one-job-per-folder layout and the lint/tsconfig rules.
  - `references/api-contract.md` adds every `/api/tasks` endpoint, `TaskDto`, the create/update rules and the `Tasks` table, plus how dev users work (hash only, don't create them).
- [x] Verified: running `scripts/create-app.mjs` into an empty folder gives the same 47 `src` files, `npm run lint` and `npm run build` pass, and the bundle hashes match this project's build.
- [ ] Later: whenever T2–T7 change the code again, mirror `src/` into the template again (`robocopy src .claude/skills/task-management-ui/assets/template/src /MIR`) and re-run the scaffold check.
- [ ] The skill's test prompts (`evals/evals.json`) still only cover the login page. Add prompts for the task table, creating a task and inline edits before the next skill benchmark.

---

## Backlog (add new work items below)

- [ ] **T9: Drag-and-drop status board** (optional, follows D3). Add a Kanban view with To do, In progress and Done columns as an alternative to the table. Use the native HTML5 drag events or `@dnd-kit/core`, and keep the dropdown as the keyboard-accessible fallback. Reuse `useTasks.updateTaskFields`.
- [ ] **T10: Edit and delete a task.** Reuse `CreateTaskForm` in edit mode (`PUT`) from a row action, and add a delete action (`DELETE /api/tasks/{id}`) with a confirmation step.
- [ ] **T11: Summary header.** Show counts from `GET /api/tasks/summary` (by status and priority) above the table. Clicking a count could set the matching column filter.
- [ ] **T12: Column sorting.** The API has no sort parameter yet and always returns newest first. Sorting by priority or due date needs a `sort` query parameter in the API first; sorting only the current page in the browser would be misleading.
