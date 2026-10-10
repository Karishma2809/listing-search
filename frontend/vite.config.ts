import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The API listens on :5080 (see backend/src/ListingSearch.Api/Properties/launchSettings.json).
// Proxying /api keeps the browser on a single origin, so no CORS setup is needed in development.
const api = { '/api': { target: 'http://localhost:5080', changeOrigin: true } };

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: api },
  preview: { port: 5173, proxy: api },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
  },
});
