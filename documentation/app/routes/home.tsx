import type { Route } from './+types/home';
import { HomeLayout } from 'fumadocs-ui/layouts/home';
import { Link } from 'react-router';
import { baseOptions } from '@/lib/layout.shared';

export function meta({}: Route.MetaArgs) {
  return [
    { title: 'ExtrabbitCode Inventor ModernUi' },
    {
      name: 'description',
      content:
        'A tiny, conflict-free WPF styling library for Autodesk Inventor add-ins — themeable controls, light/dark, window-scoped resources.',
    },
  ];
}

export default function Home() {
  return (
    <HomeLayout {...baseOptions()}>
      <main className="flex flex-col items-center justify-center flex-1 px-6 py-20 text-center gap-6">
        <img
          src={`${import.meta.env.BASE_URL}images/branding/ModernUi.png`}
          alt="ModernUi icon"
          className="w-24 h-24"
        />

        <div className="flex flex-col items-center gap-3 max-w-xl">
          <h1 className="text-3xl font-bold tracking-tight">
            ExtrabbitCode Inventor ModernUi
          </h1>
          <p className="text-fd-muted-foreground text-base leading-relaxed">
            A tiny WPF styling library for Autodesk Inventor add-ins: themeable
            controls (light/dark), themed dialogs and toasts, and a glyph set —
            all applied per-window with zero global state, so several add-ins
            can each ship their own version into one Inventor without clashing.
          </p>
        </div>

        <Link
          className="bg-fd-primary text-fd-primary-foreground rounded-full font-medium px-6 py-2.5 text-sm hover:opacity-90 transition-opacity"
          to="/docs"
        >
          Open Documentation
        </Link>
      </main>
    </HomeLayout>
  );
}
