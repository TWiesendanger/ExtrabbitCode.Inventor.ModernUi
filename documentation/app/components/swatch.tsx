function Chip({ color }: { color: string }) {
  return (
    <span className="inline-flex flex-col items-center gap-1">
      <span
        className="block w-8 h-8 rounded-md border border-fd-border"
        style={{ backgroundColor: color }}
        title={color}
      />
      <code className="text-[10px] leading-none text-fd-muted-foreground">
        {color}
      </code>
    </span>
  );
}

interface SwatchProps {
  /** Hex colour shown in the light-theme square. */
  light: string;
  /** Hex colour shown in the dark-theme square. */
  dark: string;
}

/**
 * Two colour squares - the light-theme value and the dark-theme value - each
 * with its hex below. Used in the palette table on the Theming page.
 */
export function Swatch({ light, dark }: SwatchProps) {
  return (
    <span className="inline-flex gap-3 align-middle">
      <Chip color={light} />
      <Chip color={dark} />
    </span>
  );
}
