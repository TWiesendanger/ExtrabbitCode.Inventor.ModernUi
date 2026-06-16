import type { ImgHTMLAttributes } from 'react';

function resolve(src: string): string {
  return `${import.meta.env.BASE_URL}${src.replace(/^\//, '')}`;
}

interface ThemedImageProps
  extends Omit<ImgHTMLAttributes<HTMLImageElement>, 'src'> {
  /** Base slug of the image pair, e.g. "buttons__accent-button". */
  slug: string;
  alt: string;
  /** Folder under /public the images live in. Defaults to images/controls. */
  dir?: string;
}

/**
 * Renders a screenshot that swaps with the site theme: it shows
 * `<slug>-light.png` in light mode and `<slug>-dark.png` in dark mode. The PNGs
 * are produced by the Gallery `--shoot-docs` snapshotter.
 */
export function ThemedImage({
  slug,
  alt,
  dir = 'images/controls',
  className,
  ...props
}: ThemedImageProps) {
  const light = resolve(`${dir}/${slug}-light.png`);
  const dark = resolve(`${dir}/${slug}-dark.png`);
  const base = 'rounded-lg border border-fd-border my-2';
  return (
    <>
      <img
        src={light}
        alt={alt}
        className={`${base} block dark:hidden ${className ?? ''}`}
        {...props}
      />
      <img
        src={dark}
        alt={alt}
        className={`${base} hidden dark:block ${className ?? ''}`}
        {...props}
      />
    </>
  );
}
