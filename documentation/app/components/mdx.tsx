import defaultMdxComponents from 'fumadocs-ui/mdx';
import type { MDXComponents } from 'mdx/types';
import type { ImgHTMLAttributes } from 'react';
import { ThemedImage } from './themed-image';
import { Swatch } from './swatch';

function resolvePublicSrc(src: string | undefined): string | undefined {
  if (!src?.startsWith('/')) return src;
  return `${import.meta.env.BASE_URL}${src.slice(1)}`;
}

export function getMDXComponents(components?: MDXComponents) {
  return {
    ...defaultMdxComponents,
    img: ({ src, alt, ...props }: ImgHTMLAttributes<HTMLImageElement>) => (
      <img src={resolvePublicSrc(src)} alt={alt} {...props} />
    ),
    ThemedImage,
    Swatch,
    ...components,
  } satisfies MDXComponents;
}

export const useMDXComponents = getMDXComponents;

declare global {
  type MDXProvidedComponents = ReturnType<typeof getMDXComponents>;
}
