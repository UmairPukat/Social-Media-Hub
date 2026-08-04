import { Injectable, computed, effect, signal } from '@angular/core';
import {
  CHROME_THEMES,
  CHROME_THEME_STORAGE_KEY,
  ChromeThemeId,
  DEFAULT_CHROME_THEME
} from '../theme/chrome-themes';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly themes = CHROME_THEMES;
  readonly themeId = signal<ChromeThemeId>(this.readStored());

  readonly activeTheme = computed(
    () => CHROME_THEMES.find((t) => t.id === this.themeId()) || CHROME_THEMES[0]
  );

  constructor() {
    effect(() => {
      const id = this.themeId();
      localStorage.setItem(CHROME_THEME_STORAGE_KEY, id);
      document.documentElement.setAttribute('data-chrome-theme', id);
      document.documentElement.removeAttribute('data-integration-theme');
      document.documentElement.style.removeProperty('--app-page-bg');
      document.documentElement.style.removeProperty('--app-card-bg');
      document.documentElement.style.removeProperty('--app-ink');
      document.documentElement.style.removeProperty('--app-muted');
    });
  }

  setTheme(id: ChromeThemeId): void {
    if (!CHROME_THEMES.some((t) => t.id === id)) return;
    this.themeId.set(id);
  }

  private readStored(): ChromeThemeId {
    const raw = localStorage.getItem(CHROME_THEME_STORAGE_KEY) as ChromeThemeId | null;
    if (raw && CHROME_THEMES.some((t) => t.id === raw)) return raw;
    return DEFAULT_CHROME_THEME;
  }
}
