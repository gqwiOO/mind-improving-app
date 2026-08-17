import rss from '@astrojs/rss';
import type { APIRoute } from 'astro';
import { getPosts } from '~/lib/content';
import { excerpt } from '~/lib/utils';
import { site } from '~/site';

export const GET: APIRoute = async (context) => {
  const posts = await getPosts();

  return rss({
    title: site.title,
    description: site.description,
    site: context.site ?? 'https://example.com',
    customData: `<language>${site.lang}</language>`,
    items: posts.map((post) => ({
      title: post.data.title,
      pubDate: post.data.date,
      description: post.data.description ?? excerpt(post.body),
      link: `/writing/${post.id}/`,
      categories: post.data.tags,
    })),
  });
};
