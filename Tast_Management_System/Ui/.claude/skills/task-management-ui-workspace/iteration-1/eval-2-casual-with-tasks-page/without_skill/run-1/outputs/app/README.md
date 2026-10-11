# Task Management UI

React + TypeScript + Vite front end for `TaskManagement.Api`, linted with ESLint.

## Run

1. Start the API with the **https** profile (`https://localhost:7213`). Its CORS policy only allows
   `https://localhost:5173`, which is exactly where this dev server runs.
2. `npm install`
3. `npm run dev` and open https://localhost:5173 (accept the self-signed dev certificate once).

If the API runs elsewhere, copy `.env.example` to `.env.local` and change `VITE_API_BASE_URL`.

## Scripts

- `npm run dev` – dev server on https://localhost:5173
- `npm run lint` – ESLint
- `npm run build` – type-check and production build

## What's here

- `/login` – email + password form. The username must be a valid email (whitespace is stripped,
  the button stays disabled until the email is valid). Posts to `POST /api/auth/login`.
- `/tasks` – protected, empty tasks page with a **Log out** button.
- The token is kept in `sessionStorage` and the user is signed out automatically when it expires
  (60 minutes, no refresh token).
