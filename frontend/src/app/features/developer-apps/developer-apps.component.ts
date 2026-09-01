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
import { IntegrationCategoryGroup } from '../integrations/integrations.component';
import { defaultOAuthRedirectUri, defaultWebhookRedirectUri } from '../../core/config/oauth-redirect.config';
import { formatYouTubeOAuthScopes, youtubeDefaultScopeString } from '../../core/config/oauth-scopes.config';

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
  whatsapp: 'whatsapp_business_management,whatsapp_business_messaging,business_management',
  youtube: youtubeDefaultScopeString()
};

@Component({
  selector: 'app-developer-apps',
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
  templateUrl: './developer-apps.component.html',
  styleUrl: './developer-apps.component.scss'
})
export class DeveloperAppsComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly metaAuth = inject(MetaAuthUrlService);
  private readonly menuType = MENU_TYPES.developerApp;
  readonly moduleRedirectUri = defaultOAuthRedirectUri(MENU_TYPES.developerApp);
  readonly moduleWebhookUri = defaultWebhookRedirectUri(MENU_TYPES.developerApp);

  readonly cards = signal<PlatformCard[]>([]);
  readonly message = signal('');
  readonly connecting = signal<string | null>(null);
  readonly activeCategory = signal<string>('all');

  private readonly pickerDialog = viewChild<ElementRef<HTMLDialogElement>>('pickerDialog');
  private readonly detailsDialog = viewChild<ElementRef<HTMLDialogElement>>('detailsDialog');
  private readonly configDialog = viewChild<ElementRef<HTMLDialogElement>>('configDialog');

  readonly detailsOpen = signal(false);
  readonly detailsTitle = signal('');
  readonly detailsPlatformCode = signal<string | null>(null);
  readonly details = signal<ConnectionDetails | null>(null);
  readonly detailsLoading = signal(false);
  readonly detailsError = signal('');
  readonly tokenRevealed = signal(false);
  readonly tokenCopied = signal(false);

  readonly configOpen = signal(false);
  readonly configTitle = signal('');
  readonly configPlatformCode = signal<string | null>(null);
  readonly configLoading = signal(false);
  readonly configSaving = signal(false);
  readonly configError = signal('');
  readonly configForm = signal<SaveAppConnectionConfigRequest>(this.emptyConfigForm('facebook'));
  readonly secretRevealed = signal(false);

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
      error: () => this.message.set('Failed to load app connections')
    });
  }

  setCategory(id: string): void {
    this.activeCategory.set(id);
  }

  canConnectCard(card: PlatformCard): boolean {
    return card.canConnect && !!card.hasAppConfig;
  }

  async connect(card: PlatformCard): Promise<void> {
    const code = card.code.toLowerCase() as MetaPlatform;
    if (!this.canConnectCard(card)) {
      this.message.set(`Configure ${card.displayName} before connecting.`);
      this.openConfig(card);
      return;
    }

    if (!['facebook', 'instagram', 'instagram_login', 'whatsapp', 'youtube'].includes(code)) {
      this.message.set(`${card.displayName} is coming soon.`);
      return;
    }

    this.connecting.set(code);
    this.message.set(
      code === 'youtube'
        ? `Complete Google sign-in in the popup window. This button will update when you are done.`
        : code === 'instagram_login'
          ? `Opening Instagram Login for ${card.displayName}…`
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
        this.message.set(`Signed in. Choose the ${card.displayName} page you want to manage.`);
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

  supportsConnectionDetails(code: string): boolean {
    return ['facebook', 'instagram', 'instagram_login', 'youtube', 'whatsapp'].includes(code.toLowerCase());
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

  isInstagramFbLoginPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'instagram';
  }

  instagramAccountName(info: ConnectionDetails): string {
    if (info.instagramUsername) {
      return info.instagramUsername.startsWith('@')
        ? info.instagramUsername
        : `@${info.instagramUsername}`;
    }
    const profile = info.profiles?.[0];
    if (profile?.username) {
      return profile.username.startsWith('@') ? profile.username : `@${profile.username}`;
    }
    if (profile?.name) return profile.name;
    if (info.pageName) return info.pageName;
    return '—';
  }

  readonly isInstagramLoginDetails = computed(() =>
    this.isInstagramLoginPlatform(this.details()?.platformCode) ||
    this.isInstagramLoginPlatform(this.detailsPlatformCode())
  );

  isWhatsAppPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'whatsapp';
  }

  /** Facebook, Instagram, Instagram Login, and WhatsApp use Meta webhooks on the module endpoint. */
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

    if (this.isWhatsAppPlatform(code)) {
      if (!form.phoneNumberId?.trim()) {
        this.configError.set('Phone number Id is required for WhatsApp.');
        return;
      }
      if (!form.webhookVerifyToken?.trim()) {
        this.configError.set('Webhook verify token is required for WhatsApp.');
        return;
      }
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
        this.message.set(res.message || 'App configuration saved.');
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
    if (!confirm(`Delete app configuration for ${card.displayName}?`)) return;

    this.processApi.deleteConfig(this.menuType, card.code).subscribe({
      next: (res) => {
        this.message.set(res.message || 'App configuration deleted.');
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Delete failed')
    });
  }

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

  updateConfigField<K extends keyof SaveAppConnectionConfigRequest>(
    field: K,
    value: SaveAppConnectionConfigRequest[K]
  ): void {
    this.configForm.update((form) => ({ ...form, [field]: value }));
  }

  toggleSecretReveal(): void {
    this.secretRevealed.update((v) => !v);
  }

  tokenLabel(platformCode: string): string {
    const code = (platformCode || '').toLowerCase();
    if (code === 'facebook' || code === 'instagram') return 'Page access token';
    if (code === 'instagram_login') return 'Instagram access token';
    if (code === 'youtube') return 'Google access token';
    if (code === 'whatsapp') return 'WhatsApp access token';
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
      whatsapp: 'green'
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
      whatsapp: 'chat'
    };
    return map[code.toLowerCase()] || 'extension';
  }
}
