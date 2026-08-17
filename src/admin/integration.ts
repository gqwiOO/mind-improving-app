import type { AstroIntegration } from 'astro';
import { createAdminHandler } from './handler';

/**
 * Адмінка для локального редагування контенту.
 *
 * Живе тільки в `astro dev`: middleware чіпляється в хук `astro:server:setup`,
 * який під час `astro build` не викликається взагалі. Тобто в `dist/` не
 * потрапляє ні сторінка /admin, ні API — а отже, опублікований сайт лишається
 * звичайною статикою без жодного способу щось у ньому змінити.
 */
export function adminPanel(): AstroIntegration {
  let markdownConfig: Parameters<typeof createAdminHandler>[0];

  return {
    name: 'admin-panel',
    hooks: {
      'astro:config:done': ({ config }) => {
        // Беремо markdown-налаштування сайту, щоб передперегляд збігався один в один
        markdownConfig = config.markdown as Parameters<typeof createAdminHandler>[0];
      },

      'astro:server:setup': ({ server, logger }) => {
        const handle = createAdminHandler(markdownConfig);

        server.middlewares.use((req, res, next) => {
          handle(req, res).then(
            (handled) => {
              if (!handled) next();
            },
            (error: unknown) => {
              logger.error(String(error));
              res.writeHead(500, { 'content-type': 'application/json; charset=utf-8' });
              res.end(JSON.stringify({ error: 'Внутрішня помилка адмінки' }));
            },
          );
        });

        logger.info('Адмінка: http://localhost:' + (server.config.server.port ?? 4321) + '/admin');
      },
    },
  };
}
