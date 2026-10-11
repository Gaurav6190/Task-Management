# Task Management UI

React 19 + TypeScript + Vite front end for `TaskManagement.Api` (in `../Api`). Linted with ESLint.

## Run

1. Start the API with the **https** profile (`https://localhost:7213`). Its CORS policy only allows
   the origin `https://localhost:5173`. Login only works once a dev user is configured with
   `Api/scripts/Set-DevAuthUser.ps1` (Development only).
2. `npm install`
3. `npm run dev`, then open https://localhost:5173 (accept the self-signed dev certificate once).

| Script            | What it does                         |
| ----------------- | ------------------------------------ |
| `npm run dev`     | Dev server on https://localhost:5173 |
| `npm run lint`    | ESLint                               |
| `npm run build`   | Type-check (`tsc -b`) + production build |
| `npm run preview` | Serve the production build           |

## Configuration

`VITE_API_BASE_URL` (in `.env.development`) is the API base URL. Default: `https://localhost:7213`.

## Login

- `POST /api/auth/login` with `{ username, password }`; the username must be an email address.
- Client-side checks mirror the API's `LoginCommandValidator` (required, valid email, ≤ 256 / ≤ 128 chars);
  server validation errors (`errors.Username` / `errors.Password`) are shown on the fields and a 401 shows
  "Invalid email or password."
- The returned JWT (60 minutes, no refresh) is kept in `sessionStorage` and dropped once expired.
