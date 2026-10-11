import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react()],
    server: {
      port: 5173,
      // TaskManagement.Api only allows CORS from https://localhost:5173. Proxying /api keeps the
      // browser on the dev server's origin, so CORS never comes into play during development.
      proxy: {
        '/api': {
          target: env.API_PROXY_TARGET || 'https://localhost:7213',
          changeOrigin: true,
          secure: false, // the ASP.NET Core development certificate is self-signed
        },
      },
    },
  }
})
