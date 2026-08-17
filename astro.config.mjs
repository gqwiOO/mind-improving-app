// @ts-check
import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import { adminPanel } from './src/admin/integration';

// Заміни на свій домен, коли будеш деплоїти.
// Від цього значення залежать посилання в RSS і sitemap.
const SITE = 'https://example.com';

export default defineConfig({
  site: SITE,
  // adminPanel живе тільки в `astro dev` — у зібраний сайт не потрапляє
  integrations: [sitemap(), adminPanel()],
  markdown: {
    shikiConfig: {
      themes: { light: 'github-light', dark: 'github-dark' },
      wrap: true,
    },
  },
});
