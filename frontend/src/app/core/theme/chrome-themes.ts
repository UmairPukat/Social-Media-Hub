export type ChromeThemeId = 'azure' | 'graphite' | 'ocean' | 'navy' | 'emerald';

export interface ChromeTheme {
  id: ChromeThemeId;
  name: string;
  description: string;
  /** Preview swatches shown in Settings. */
  preview: {
    navbar: string;
    sidebar: string;
    accent: string;
  };
}

export const CHROME_THEMES: ChromeTheme[] = [
  {
    id: 'azure',
    name: 'Azure Clean',
    description: 'Bright white chrome with soft blue accents — crisp and modern.',
    preview: { navbar: '#ffffff', sidebar: '#f8fafc', accent: '#2563eb' }
  },
  {
    id: 'graphite',
    name: 'Graphite Pro',
    description: 'Light navbar paired with a charcoal sidebar for strong contrast.',
    preview: { navbar: '#ffffff', sidebar: '#1c1917', accent: '#2563eb' }
  },
  {
    id: 'ocean',
    name: 'Ocean Depth',
    description: 'Cool ice navbar with a deep teal sidebar — calm and focused.',
    preview: { navbar: '#f0f9ff', sidebar: '#0f4c4a', accent: '#0d9488' }
  },
  {
    id: 'navy',
    name: 'Executive Navy',
    description: 'Professional dark navy chrome for a polished command center feel.',
    preview: { navbar: '#0f172a', sidebar: '#111827', accent: '#38bdf8' }
  },
  {
    id: 'emerald',
    name: 'Emerald Soft',
    description: 'Soft white surfaces with refined green accents — fresh and calm.',
    preview: { navbar: '#ffffff', sidebar: '#f3faf6', accent: '#059669' }
  }
];

export const DEFAULT_CHROME_THEME: ChromeThemeId = 'azure';
export const CHROME_THEME_STORAGE_KEY = 'smh_chrome_theme';
