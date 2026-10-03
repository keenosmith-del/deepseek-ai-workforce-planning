import { defineConfig } from "vite";
export default defineConfig({
  server: {
    proxy: {
      "/api": "http://localhost:5180",
      "/health": "http://localhost:5180",
    },
  },
});
