import basicSsl from '@vitejs/plugin-basic-ssl'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
// The API only allows CORS from https://localhost:5173 (Api appsettings.json -> Cors:AllowedOrigins),
// so the dev server must run on exactly that origin: HTTPS, port 5173, no fallback port.
export default defineConfig({
  plugins: [react(), basicSsl()],
  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
  },
  preview: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
  },
})
