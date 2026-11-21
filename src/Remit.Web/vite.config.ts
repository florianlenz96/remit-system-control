import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// The built app is served by the ASP.NET Core backend (ADR 0008): one container, one origin.
export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../Remit.App/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: { '/api': 'http://localhost:5000' },
  },
  test: {
    environment: 'node',
  },
})
