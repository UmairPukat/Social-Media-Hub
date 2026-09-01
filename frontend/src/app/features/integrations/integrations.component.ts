import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProcessApiService } from '../../core/services/process-api.service';
import { MetaAuthUrlService, MetaPlatform } from '../../core/services/meta-auth-url.service';
import {
  ApiResponse,
  AppConnectionConfig,
  ConnectionDetails,
  MENU_TYPES,
  MetaPage,
  PlatformCard,
  SaveAppConnectionConfigRequest
} from '../../core/models/api.models';
import { PROCESS_MODULES } from '../../core/config/process.config';
import { defaultOAuthRedirectUri, defaultWebhookRedirectUri } from '../../core/config/oauth-redirect.config';
import { formatYouTubeOAuthScopes, youtubeDefaultScopeString } from '../../core/config/oauth-scopes.config';
import { instagramAccountName, instagramDisplayName } from '../../core/utils/connection-details.util';

export interface IntegrationCategoryGroup {
  id: string;
  label: string;
  accent: string;
  icon: string;
  items: PlatformCard[];
}

const CATEGORY_META: Record<string, { accent: string; icon: string }> = {
  social: { accent: '#2563eb', icon: 'share' },
  communication: { accent: '#16a34a', icon: 'forum' },
  commerce: { accent: '#ea580c', icon: 'storefront' },
  crm: { accent: '#7c3aed', icon: 'groups' },
  calendar: { accent: '#0891b2', icon: 'calendar_month' },
  storage: { accent: '#475569', icon: 'cloud' },
  payment: { accent: '#0f766e', icon: 'payments' },
  ai: { accent: '#db2777', icon: 'auto_awesome' }
};

const CATEGORY_ORDER = [
  'social',
  'communication',
  'commerce',
  'crm',
  'calendar',
  'storage',
  'payment',
  'ai'
];

const META_PLATFORMS = new Set(['facebook', 'instagram', 'instagram_login', 'whatsapp', 'youtube']);

const DEFAULT_AUTH_URLS: Record<string, string> = {
  instagram_login: 'https://www.instagram.com/oauth/authorize',
  facebook: 'https://www.facebook.com/v21.0/dialog/oauth',
  instagram: 'https://www.facebook.com/v21.0/dialog/oauth',
  whatsapp: 'https://www.facebook.com/v21.0/dialog/oauth',
  youtube: 'https://accounts.google.com/o/oauth2/v2/auth'
};

const DEFAULT_BASE_URLS: Record<string, string> = {
  instagram_login: 'https://graph.instagram.com',
  facebook: 'https://graph.facebook.com',
  instagram: 'https://graph.facebook.com',
  whatsapp: 'https://graph.facebook.com',
  youtube: 'https://www.googleapis.com/youtube/v3'
};

const DEFAULT_SCOPES: Record<string, string> = {
  youtube: youtubeDefaultScopeString()
};

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule
  ],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly metaAuth = inject(MetaAuthUrlService);
  private readonly menuType = MENU_TYPES.integration;
  readonly moduleRedirectUri = defaultOAuthRedirectUri(MENU_TYPES.integration);
  readonly moduleWebhookUri = defaultWebhookRedirectUri(MENU_TYPES.integration);
  readonly instagramAccountName = instagramAccountName;
  readonly instagramDisplayName = instagramDisplayName;

  readonly cards = signal<PlatformCard[]>([]);
  readonly message = signal('');
  readonly connecting = signal<string | null>(null);
  readonly activeCategory = signal<string>('all');

  /**
   * Native dialog so the picker lands in the browser top layer and stays centred in the
   * viewport — a `position: fixed` panel would anchor to the transformed `.page` wrapper.
   */
  private readonly pickerDialog = viewChild<ElementRef<HTMLDialogElement>>('pickerDialog');
  private readonly detailsDialog = viewChild<ElementRef<HTMLDialogElement>>('detailsDialog');
  private readonly configDialog = viewChild<ElementRef<HTMLDialogElement>>('configDialog');

  readonly configOpen = signal(false);
  readonly configTitle = signal('');
  readonly configPlatformCode = signal<string | null>(null);
  readonly configLoading = signal(false);
  readonly configSaving = signal(false);
  readonly configError = signal('');
  readonly configForm = signal<SaveAppConnectionConfigRequest>(this.emptyConfigForm('facebook'));
  readonly secretRevealed = signal(false);

  /** Account information popup opened from the eye icon on a connected card. */
  readonly detailsOpen = signal(false);
  readonly detailsTitle = signal('');
  readonly detailsPlatformCode = signal<string | null>(null);
  readonly details = signal<ConnectionDetails | null>(null);
  readonly detailsLoading = signal(false);
  readonly detailsError = signal('');
  readonly tokenRevealed = signal(false);
  readonly tokenCopied = signal(false);

  /** Page picker state — shown after Meta login so one page is connected at a time. */
  readonly pickerPlatform = signal<MetaPlatform | null>(null);
  readonly pickerTitle = signal('');
  readonly pages = signal<MetaPage[]>([]);
  readonly pagesLoading = signal(false);
  readonly pagesError = signal('');
  readonly selectedPageId = signal<string | null>(null);
  readonly savingPage = signal(false);

  readonly connectedCount = computed(() => this.cards().filter((c) => c.isConnected).length);

  readonly eligiblePages = computed(() => this.pages().filter((p) => p.isEligible));

  readonly categories = computed<IntegrationCategoryGroup[]>(() => {
    const map = new Map<string, IntegrationCategoryGroup>();
    for (const card of this.cards()) {
      const id = (card.category || 'other').toLowerCase();
      if (!map.has(id)) {
        const meta = CATEGORY_META[id] || { accent: '#64748b', icon: 'extension' };
        map.set(id, {
          id,
          label: card.categoryLabel || id,
          accent: meta.accent,
          icon: meta.icon,
          items: []
        });
      }
      map.get(id)!.items.push(card);
    }

    return CATEGORY_ORDER
      .map((id) => map.get(id))
      .filter((g): g is IntegrationCategoryGroup => !!g && g.items.length > 0)
      .concat([...map.values()].filter((g) => !CATEGORY_ORDER.includes(g.id)));
  });

  readonly visibleCategories = computed(() => {
    const active = this.activeCategory();
    if (active === 'all') return this.categories();
    return this.categories().filter((c) => c.id === active);
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.processApi.getPlatforms(this.menuType).subscribe({
      next: (res: ApiResponse<PlatformCard[]>) => this.cards.set(res.data || []),
      error: () => this.message.set('Failed to load integrations')
    });
  }

  setCategory(id: string): void {
    this.activeCategory.set(id);
  }

  supportsMetaConfig(code: string): boolean {
    return META_PLATFORMS.has(code.toLowerCase());
  }

  canConnectCard(card: PlatformCard): boolean {
    const code = card.code.toLowerCase();
    if (!card.canConnect || !this.supportsMetaConfig(code)) return false;
    return !!card.hasAppConfig;
  }

  isWhatsAppPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'whatsapp';
  }

  usesMetaWebhooks(code: string | null | undefined): boolean {
    const value = (code || '').toLowerCase();
    return value === 'facebook'
      || value === 'instagram'
      || value === 'instagram_login'
      || value === 'whatsapp';
  }

  isYouTubePlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'youtube';
  }

  openConfig(card: PlatformCard): void {
    const code = card.code.toLowerCase();
    this.configTitle.set(card.displayName);
    this.configPlatformCode.set(code);
    this.configError.set('');
    this.secretRevealed.set(false);
    this.configLoading.set(true);
    this.configOpen.set(true);

    const dialog = this.configDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();

    if (card.hasAppConfig) {
      this.processApi.getConfig(this.menuType, code, true).subscribe({
        next: (res) => {
          this.configLoading.set(false);
          if (!res.success || !res.data) {
            this.configForm.set(this.emptyConfigForm(code));
            this.configError.set(res.message || 'Could not load configuration.');
            return;
          }
          this.configForm.set(this.mapConfigToForm(res.data as AppConnectionConfig));
        },
        error: () => {
          this.configLoading.set(false);
          this.configForm.set(this.emptyConfigForm(code));
          this.configError.set('Could not load configuration.');
        }
      });
    } else {
      this.configLoading.set(false);
      this.configForm.set(this.emptyConfigForm(code));
    }
  }

  closeConfig(): void {
    const dialog = this.configDialog()?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      return;
    }
    this.resetConfig();
  }

  onConfigClosed(): void {
    this.resetConfig();
  }

  private resetConfig(): void {
    this.configOpen.set(false);
    this.configPlatformCode.set(null);
    this.configError.set('');
    this.secretRevealed.set(false);
  }

  saveConfig(): void {
    const code = this.configPlatformCode();
    if (!code) return;

    const form = this.configForm();
    if (!form.clientId?.trim()) {
      this.configError.set('Client Id is required.');
      return;
    }

    this.configSaving.set(true);
    this.configError.set('');

    this.processApi.saveConfig(this.menuType, {
      ...form,
      platformCode: code,
      menuType: this.menuType,
      redirectUri: this.moduleRedirectUri,
      scopes: this.isYouTubePlatform(code) ? formatYouTubeOAuthScopes(form.scopes) : form.scopes
    }).subscribe({
      next: (res) => {
        this.configSaving.set(false);
        if (!res.success) {
          this.configError.set(res.message || 'Could not save configuration.');
          return;
        }
        this.message.set(res.message || 'Integration configuration saved.');
        this.closeConfig();
        this.reload();
      },
      error: (err: { error?: { message?: string } }) => {
        this.configSaving.set(false);
        this.configError.set(err?.error?.message || 'Could not save configuration.');
      }
    });
  }

  deleteConfig(card: PlatformCard): void {
    if (!card.hasAppConfig) return;
    if (!confirm(`Delete integration configuration for ${card.displayName}?`)) return;

    this.processApi.deleteConfig(this.menuType, card.code).subscribe({
      next: (res) => {
        this.message.set(res.message || 'Integration configuration deleted.');
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Delete failed')
    });
  }

  updateConfigField<K extends keyof SaveAppConnectionConfigRequest>(
    field: K,
    value: SaveAppConnectionConfigRequest[K]
  ): void {
    this.configForm.update((form) => ({ ...form, [field]: value }));
  }

  toggleSecretReveal(): void {
    this.secretRevealed.update((v) => !v);
  }

  async connect(card: PlatformCard): Promise<void> {
    const code = card.code.toLowerCase() as MetaPlatform;
    if (!card.canConnect || !this.supportsMetaConfig(code)) {
      this.message.set(`${card.displayName} is coming soon.`);
      return;
    }

    if (!this.canConnectCard(card)) {
      this.message.set(`Configure ${card.displayName} before connecting.`);
      this.openConfig(card);
      return;
    }

    this.connecting.set(code);
    this.message.set(
      code === 'instagram_login'
        ? `Opening Instagram Login for ${card.displayName}…`
        : code === 'youtube'
          ? `Complete Google sign-in in the popup window. This button will update when you are done.`
          : `Opening Meta login for ${card.displayName}…`
    );

    try {
      const result = await this.metaAuth.openPopup(code, this.menuType);
      if (!result.ok) {
        this.message.set(result.message || 'Connection failed');
        return;
      }

      this.reload();

      if (this.supportsPageSelection(code)) {
        this.message.set(`Signed in with Meta. Choose the ${card.displayName} page you want to manage.`);
        this.openPagePicker(card);
      } else {
        this.message.set(`${card.displayName} connected.`);
      }
    } catch (err) {
      this.message.set(err instanceof Error ? err.message : 'Connection failed');
    } finally {
      this.connecting.set(null);
    }
  }

  supportsPageSelection(code: string): boolean {
    return ['facebook', 'instagram'].includes(code.toLowerCase());
  }

  /** Eye icon / account details popup — includes Instagram Login (no page picker). */
  supportsConnectionDetails(code: string): boolean {
    return ['facebook', 'instagram', 'instagram_login', 'youtube'].includes(code.toLowerCase());
  }

  isInstagramLoginPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'instagram_login';
  }

  isInstagramPlatform(code: string | null | undefined): boolean {
    const value = (code || '').toLowerCase();
    return value === 'instagram' || value === 'instagram_login';
  }

  isFacebookPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'facebook';
  }

  /** Instagram connected via Facebook Login (page picker), not Instagram Login API. */
  isInstagramFbLoginPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'instagram';
  }

  readonly isInstagramLoginDetails = computed(() =>
    this.isInstagramLoginPlatform(this.details()?.platformCode) ||
    this.isInstagramLoginPlatform(this.detailsPlatformCode())
  );

  openPagePicker(card: PlatformCard): void {
    const code = card.code.toLowerCase() as MetaPlatform;
    this.pickerPlatform.set(code);
    this.pickerTitle.set(card.displayName);
    this.loadPages();

    const dialog = this.pickerDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();
  }

  closePagePicker(): void {
    const dialog = this.pickerDialog()?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      return;
    }
    this.resetPicker();
  }

  /** Also runs when the dialog is dismissed with Escape. */
  onPickerClosed(): void {
    this.resetPicker();
  }

  private resetPicker(): void {
    this.pickerPlatform.set(null);
    this.pages.set([]);
    this.pagesError.set('');
    this.selectedPageId.set(null);
  }

  loadPages(): void {
    const code = this.pickerPlatform();
    if (!code) return;

    this.pagesLoading.set(true);
    this.pagesError.set('');
    this.processApi.getMetaPages(this.menuType, code).subscribe({
      next: (res: ApiResponse<MetaPage[]>) => {
        this.pagesLoading.set(false);
        if (!res.success) {
          this.pagesError.set(res.message || 'Could not load your pages.');
          return;
        }
        const list = res.data || [];
        this.pages.set(list);
        this.selectedPageId.set(list.find((p) => p.isSelected && p.isEligible)?.pageId || null);
      },
      error: (err: { error?: { message?: string } }) => {
        this.pagesLoading.set(false);
        this.pagesError.set(err?.error?.message || 'Could not load your pages.');
      }
    });
  }

  /** Checkboxes behave as a single-choice list: ticking one clears the rest. */
  togglePage(page: MetaPage): void {
    if (!page.isEligible) return;
    this.selectedPageId.update((current) => (current === page.pageId ? null : page.pageId));
  }

  isPageChecked(page: MetaPage): boolean {
    return this.selectedPageId() === page.pageId;
  }

  confirmPage(): void {
    const code = this.pickerPlatform();
    const pageId = this.selectedPageId();
    if (!code || !pageId) return;

    this.savingPage.set(true);
    this.processApi.selectMetaPage(this.menuType, code, pageId).subscribe({
      next: (res) => {
        this.savingPage.set(false);
        if (!res.success) {
          this.pagesError.set(res.message || 'Could not connect that page.');
          return;
        }
        this.message.set(res.message || 'Page connected.');
        this.closePagePicker();
        this.reload();
      },
      error: (err: { error?: { message?: string } }) => {
        this.savingPage.set(false);
        this.pagesError.set(err?.error?.message || 'Could not connect that page.');
      }
    });
  }

  openDetails(card: PlatformCard): void {
    this.detailsTitle.set(card.displayName);
    this.detailsPlatformCode.set(card.code.toLowerCase());
    this.details.set(null);
    this.detailsError.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
    this.detailsLoading.set(true);
    this.detailsOpen.set(true);

    const dialog = this.detailsDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();

    this.processApi.getConnectionDetails(this.menuType, card.code).subscribe({
      next: (res: ApiResponse<ConnectionDetails>) => {
        this.detailsLoading.set(false);
        if (!res.success) {
          this.detailsError.set(res.message || 'Could not load account details.');
          return;
        }
        this.details.set(res.data);
      },
      error: (err: { error?: { message?: string } }) => {
        this.detailsLoading.set(false);
        this.detailsError.set(err?.error?.message || 'Could not load account details.');
      }
    });
  }

  closeDetails(): void {
    const dialog = this.detailsDialog()?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      return;
    }
    this.resetDetails();
  }

  onDetailsClosed(): void {
    this.resetDetails();
  }

  private resetDetails(): void {
    this.detailsOpen.set(false);
    this.detailsPlatformCode.set(null);
    this.details.set(null);
    this.detailsError.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
  }

  tokenLabel(platformCode: string): string {
    const code = (platformCode || '').toLowerCase();
    if (code === 'facebook' || code === 'instagram') return 'Page access token';
    if (code === 'instagram_login') return 'Instagram access token';
    if (code === 'youtube') return 'Google access token';
    return 'Access token';
  }

  youtubeChannelName(info: ConnectionDetails): string {
    return info.pageName || info.profiles?.[0]?.name || info.accountName || '—';
  }

  youtubeChannelId(info: ConnectionDetails): string {
    return info.pageId || info.profiles?.[0]?.externalProfileId || '—';
  }

  maskedToken(token: string): string {
    if (token.length <= 12) return '••••••••';
    return `${token.slice(0, 6)}…${token.slice(-4)}`;
  }

  toggleTokenReveal(): void {
    this.tokenRevealed.update((v) => !v);
  }

  async copyToken(token: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(token);
      this.tokenCopied.set(true);
      window.setTimeout(() => this.tokenCopied.set(false), 1600);
    } catch {
      this.tokenCopied.set(false);
    }
  }

  disconnect(card: PlatformCard): void {
    this.processApi.disconnect(this.menuType, card.code).subscribe({
      next: () => {
        this.message.set(`${card.displayName} disconnected.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Disconnect failed')
    });
  }

  private emptyConfigForm(platformCode: string): SaveAppConnectionConfigRequest {
    const code = platformCode.toLowerCase();
    return {
      platformCode: code,
      menuType: this.menuType,
      clientId: '',
      clientSecret: '',
      redirectUri: this.moduleRedirectUri,
      authUrl: DEFAULT_AUTH_URLS[code] || '',
      baseUrl: DEFAULT_BASE_URLS[code] || 'https://graph.facebook.com',
      scopes: DEFAULT_SCOPES[code] || '',
      graphApiVersion: 'v21.0',
      webhookVerifyToken: '',
      phoneNumberId: '',
      wabaId: ''
    };
  }

  private mapConfigToForm(config: AppConnectionConfig): SaveAppConnectionConfigRequest {
    return {
      platformCode: config.platformCode,
      menuType: this.menuType,
      label: config.label,
      clientId: config.clientId,
      clientSecret: config.clientSecret,
      redirectUri: this.moduleRedirectUri,
      authUrl: config.authUrl,
      baseUrl: config.baseUrl,
      scopes: config.scopes
        ? this.isYouTubePlatform(config.platformCode)
          ? formatYouTubeOAuthScopes(config.scopes)
          : config.scopes
        : DEFAULT_SCOPES[config.platformCode.toLowerCase()] || '',
      graphApiVersion: config.graphApiVersion,
      webhookVerifyToken: config.webhookVerifyToken,
      phoneNumberId: config.phoneNumberId,
      wabaId: config.wabaId
    };
  }

  tone(code: string): string {
    const map: Record<string, string> = {
      facebook: 'blue',
      instagram: 'rose',
      instagram_login: 'rose',
      threads: 'slate',
      twitter: 'slate',
      x: 'slate',
      linkedin: 'cyan',
      tiktok: 'slate',
      youtube: 'rose',
      pinterest: 'rose',
      reddit: 'orange',
      snapchat: 'amber',
      whatsapp: 'green',
      outlook: 'blue',
      gmail: 'rose',
      microsoft365: 'blue',
      exchange: 'blue',
      telegram: 'cyan',
      discord: 'violet',
      slack: 'violet',
      teams: 'violet',
      shopify: 'green',
      woocommerce: 'violet',
      tiktokshop: 'pink',
      amazon: 'amber',
      etsy: 'orange',
      ebay: 'blue',
      salesforce: 'cyan',
      hubspot: 'orange',
      zoho: 'teal',
      dynamics365: 'blue',
      pipedrive: 'green',
      googlecalendar: 'blue',
      outlookcalendar: 'cyan',
      applecalendar: 'slate',
      onedrive: 'blue',
      googledrive: 'amber',
      dropbox: 'blue',
      sharepoint: 'teal',
      stripe: 'violet',
      paypal: 'blue',
      square: 'slate',
      openai: 'teal',
      azureopenai: 'blue',
      claude: 'orange',
      gemini: 'pink'
    };
    return map[code.toLowerCase()] || 'slate';
  }

  shortDesc(text: string): string {
    const clean = (text || '').trim();
    if (clean.length <= 78) return clean;
    return `${clean.slice(0, 76)}…`;
  }

  platformIcon(code: string): string {
    const map: Record<string, string> = {
      facebook: 'public',
      instagram: 'photo_camera',
      instagram_login: 'camera_alt',
      threads: 'tag',
      twitter: 'alternate_email',
      x: 'alternate_email',
      linkedin: 'work',
      tiktok: 'music_note',
      youtube: 'play_circle',
      pinterest: 'push_pin',
      reddit: 'forum',
      snapchat: 'photo_camera_front',
      whatsapp: 'chat',
      outlook: 'mail',
      gmail: 'mail_outline',
      microsoft365: 'apps',
      exchange: 'inbox',
      telegram: 'send',
      discord: 'sports_esports',
      slack: 'tag',
      teams: 'groups',
      shopify: 'storefront',
      woocommerce: 'shopping_bag',
      tiktokshop: 'store',
      amazon: 'local_shipping',
      etsy: 'store',
      ebay: 'gavel',
      salesforce: 'cloud',
      hubspot: 'hub',
      zoho: 'business',
      dynamics365: 'apartment',
      pipedrive: 'trending_up',
      googlecalendar: 'calendar_month',
      outlookcalendar: 'event',
      applecalendar: 'event_available',
      onedrive: 'cloud_queue',
      googledrive: 'add_to_drive',
      dropbox: 'folder',
      sharepoint: 'folder_shared',
      stripe: 'credit_card',
      paypal: 'account_balance_wallet',
      square: 'payments',
      openai: 'auto_awesome',
      azureopenai: 'cloud',
      claude: 'psychology',
      gemini: 'auto_awesome'
    };
    return map[code.toLowerCase()] || 'extension';
  }
}
