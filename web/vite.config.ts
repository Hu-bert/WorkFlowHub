import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

const apiUrl = 'http://localhost:5080'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': apiUrl,
      '/hubs': { target: apiUrl, ws: true },
    },
  },
  build: {
    // API serwuje zbudowany frontend jako pliki statyczne.
    outDir: '../src/FlowHub.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
  },
})
