export type ChromeThemeId =
  | 'azure'
  | 'graphite'
  | 'ocean'
  | 'navy'
  | 'emerald'
  | 'royal'
  | 'burgundy'
  | 'midnight'
  | 'copper'
  | 'frost';

export interface ChromeTheme {
  id: ChromeThemeId;
  name: string;
  description: string;
  /** Preview swatches shown in Settings. */
  preview: {
    navbar: string;
    sidebar: string;
    main: string;
    accent: string;
  };
}

export const CHROME_THEMES: ChromeTheme[] = [
  {
    id: 'azure',
    name: 'Azure Clean',
    description: 'Bright white chrome with soft blue accents — crisp and modern.',
    preview: { navbar: '#ffffff', sidebar: '#f8fafc', main: '#eef2f7', accent: '#2563eb' }
  },
  {
    id: 'graphite',
    name: 'Graphite Pro',
    description: 'Light navbar paired with a charcoal sidebar for strong contrast.',
    preview: { navbar: '#ffffff', sidebar: '#1c1917', main: '#e7e5e4', accent: '#2563eb' }
  },
  {
    id: 'ocean',
    name: 'Ocean Depth',
    description: 'Cool ice navbar with a deep teal sidebar — calm and focused.',
    preview: { navbar: '#f0f9ff', sidebar: '#0f4c4a', main: '#e0f2f1', accent: '#0d9488' }
  },
  {
    id: 'navy',
    name: 'Executive Navy',
    description: 'Professional dark navy chrome for a polished command center feel.',
    preview: { navbar: '#0f172a', sidebar: '#111827', main: '#0b1220', accent: '#38bdf8' }
  },
  {
    id: 'emerald',
    name: 'Emerald Soft',
    description: 'Soft white surfaces with refined green accents — fresh and calm.',
    preview: { navbar: '#ffffff', sidebar: '#f3faf6', main: '#e8f5ed', accent: '#059669' }
  },
  {
    id: 'royal',
    name: 'Royal Amethyst',
    description: 'Pearl navbar and deep plum sidebar with a sophisticated violet accent.',
    preview: { navbar: '#fdfcff', sidebar: '#2e1065', main: '#eee8ff', accent: '#7c3aed' }
  },
  {
    id: 'burgundy',
    name: 'Burgundy Reserve',
    description: 'Warm ivory chrome paired with rich wine tones for an executive finish.',
    preview: { navbar: '#fffbf7', sidebar: '#4c0519', main: '#fbe9e9', accent: '#be123c' }
  },
  {
    id: 'midnight',
    name: 'Midnight Indigo',
    description: 'Immersive dark indigo surfaces with luminous periwinkle details.',
    preview: { navbar: '#17152b', sidebar: '#11101f', main: '#0e0d1b', accent: '#818cf8' }
  },
  {
    id: 'copper',
    name: 'Slate & Copper',
    description: 'Cool slate chrome elevated by understated copper highlights.',
    preview: { navbar: '#1e293b', sidebar: '#0f172a', main: '#111827', accent: '#f59e0b' }
  },
  {
    id: 'frost',
    name: 'Nordic Frost',
    description: 'Quiet blue-gray surfaces with a crisp arctic blue accent.',
    preview: { navbar: '#f8fafc', sidebar: '#e8eef5', main: '#e5edf4', accent: '#0284c7' }
  }
];

export const DEFAULT_CHROME_THEME: ChromeThemeId = 'azure';
export const CHROME_THEME_STORAGE_KEY = 'smh_chrome_theme';
