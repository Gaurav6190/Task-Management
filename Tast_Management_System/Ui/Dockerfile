# syntax=docker/dockerfile:1.7
# Task Management UI - multi-stage build. Build context: ./Ui

# ---------- dependencies (cached until package*.json changes) ----------
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund

# ---------- build ----------
FROM node:24-alpine AS build
WORKDIR /app
# Empty = call the API on the same origin (/api), which nginx proxies to the API container.
# Set it only if the API is served from a different domain.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

# ---------- runtime: static files served by non-root nginx ----------
FROM nginxinc/nginx-unprivileged:1.28-alpine AS final
# Where nginx forwards /api requests, and the DNS resolver used to find it (Docker's embedded DNS).
ENV API_UPSTREAM=http://api:8080 \
    NGINX_RESOLVER=127.0.0.11
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
