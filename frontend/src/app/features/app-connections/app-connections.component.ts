import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/services/api.service';
import { AppConnectionAuthService } from '../../core/services/app-connection-auth.service';
import {
  ApiResponse,
  AppConnectionDetails,
  CreateMetaAppConnectionRequest,
  MetaAppConnection,
  MetaPage,
  UpdateMetaAppConnectionRequest
} from '../../core/models/api.models';
import {
  APP_CONNECTION_PLATFORM_META,
  APP_CONNECTION_PLATFORM_ORDER,
  IntegrationCardView,
  IntegrationPlatformGroup
} from '../../shared/integration-card.model';
import {
  integrationPlatformIcon,
  integrationShortDesc,
  integrationTone,
  supportsIntegrationConnectionDetails,
  supportsIntegrationPageSelection
} from '../../shared/integration-ui.utils';
import { environment } from '../../../environments/environment';

const PLATFORM_OPTIONS = [
  { code: 'facebook', label: 'Facebook' },
  { code: 'instagram', label: 'Instagram (Facebook Login)' },
  { code: 'instagram_login', label: 'Instagram Login' },
  { code: 'whatsapp', label: 'WhatsApp Business' }
];

const DEFAULT_BASE_URLS: Record<string, string> = {
  facebook: 'https://graph.facebook.com',
  instagram: 'https://graph.facebook.com',
  instagram_login: 'https://graph.instagram.com',
  whatsapp: 'https://graph.facebook.com'
};

@Component({
  selector: 'app-app-connections',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './app-connections.component.html',
  styleUrl: './app-connections.component.scss'
})
export class AppConnectionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly appAuth = inject(AppConnectionAuthService);

  readonly connections = signal<MetaAppConnection[]>([]);
  readonly message = signal('');
  readonly connecting = signal<string | null>(null);
  readonly savingForm = signal(false);
  readonly activeCategory = signal<string>('all');

  readonly defaultCallbackUrl = `${environment.apiUrl}/AppConnections/Callback`;

  private readonly pickerDialog = viewChild<ElementRef<HTMLDialogElement>>('pickerDialog');
  private readonly detailsDialog = viewChild<ElementRef<HTMLDialogElement>>('detailsDialog');
  private readonly formDialog = viewChild<ElementRef<HTMLDialogElement>>('formDialog');

  readonly detailsOpen = signal(false);
  readonly detailsTitle = signal('');
  readonly details = signal<AppConnectionDetails | null>(null);
  readonly detailsLoading = signal(false);
  readonly detailsError = signal('');
  readonly tokenRevealed = signal(false);
  readonly tokenCopied = signal(false);

  readonly pickerConnection = signal<MetaAppConnection | null>(null);
  readonly pages = signal<MetaPage[]>([]);
  readonly pagesLoading = signal(false);
  readonly pagesError = signal('');
  readonly selectedPageId = signal<string | null>(null);
  readonly savingPage = signal(false);

  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formName = '';
  formDescription = '';
  formBaseUrl = DEFAULT_BASE_URLS['facebook'];
  formPlatformCode = 'facebook';
  formAppId = '';
  formAppSecret = '';
  formCallbackUrl = this.defaultCallbackUrl;
  formGraphApiVersion = 'v21.0';
  formScopes = '';

  readonly connectedCount = computed(() => this.connections().filter((c) => c.isConnected).length);
  readonly eligiblePages = computed(() => this.pages().filter((p) => p.isEligible));

  readonly categories = computed<IntegrationPlatformGroup[]>(() => {
    const map = new Map<string, IntegrationPlatformGroup>();

    for (const conn of this.connections()) {
      const id = conn.platformCode.toLowerCase();
      const meta = APP_CONNECTION_PLATFORM_META[id] || {
        label: conn.platformName,
        accent: '#64748b',
        icon: 'apps'
      };

      if (!map.has(id)) {
        map.set(id, {
          id,
          label: meta.label,
          accent: meta.accent,
          icon: meta.icon,
          items: []
        });
      }

      map.get(id)!.items.push(this.toCardView(conn));
    }

    return APP_CONNECTION_PLATFORM_ORDER
      .map((id) => map.get(id))
      .filter((g): g is IntegrationPlatformGroup => !!g && g.items.length > 0)
      .concat([...map.values()].filter((g) => !APP_CONNECTION_PLATFORM_ORDER.includes(g.id)));
  });

  readonly visibleCategories = computed(() => {
    const active = this.activeCategory();
    if (active === 'all') return this.categories();
    return this.categories().filter((c) => c.id === active);
  });

  readonly isInstagramLoginDetails = computed(() =>
    this.isInstagramLoginPlatform(this.details()?.platformCode)
  );

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    void this.reloadAsync();
  }

  private async reloadAsync(): Promise<MetaAppConnection[]> {
    try {
      const res = await firstValueFrom(this.api.getAppConnections());
      const list = res.data || [];
      this.connections.set(list);
      return list;
    } catch {
      this.message.set('Failed to load app connections');
      return [];
    }
  }

  setCategory(id: string): void {
    this.activeCategory.set(id);
  }

  toCardView(conn: MetaAppConnection): IntegrationCardView {
    return {
      trackId: conn.id,
      code: conn.platformCode,
      displayName: conn.name,
      description: conn.description?.trim() || conn.platformName,
      isConnected: conn.isConnected,
      canConnect: conn.canConnect,
      accountName: conn.accountName,
      connectingKey: conn.id,
      requiresPageSelection: conn.requiresPageSelection
    };
  }

  findConnection(view: IntegrationCardView): MetaAppConnection | undefined {
    return this.connections().find((c) => c.id === view.trackId);
  }

  onConnect(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) void this.connect(card);
  }

  onDisconnect(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) this.disconnect(card);
  }

  onChangePage(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) this.openPagePicker(card);
  }

  onOpenDetails(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) this.openDetails(card);
  }

  onEdit(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) this.openEditForm(card);
  }

  onDelete(view: IntegrationCardView): void {
    const card = this.findConnection(view);
    if (card) this.deleteConnection(card);
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formName = '';
    this.formDescription = '';
    this.formPlatformCode = this.resolveCreatePlatformCode();
    this.formBaseUrl = this.defaultBaseUrl(this.formPlatformCode);
    this.formAppId = '';
    this.formAppSecret = '';
    this.formCallbackUrl = this.defaultCallbackUrl;
    this.formGraphApiVersion = 'v21.0';
    this.formScopes = '';
    this.formOpen.set(true);
    this.loadDefaultScopes(this.formPlatformCode);
    const dialog = this.formDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();
  }

  platformLabel(code: string): string {
    return PLATFORM_OPTIONS.find((p) => p.code === code)?.label ?? code;
  }

  defaultBaseUrl(platformCode: string): string {
    return DEFAULT_BASE_URLS[platformCode] ?? DEFAULT_BASE_URLS['facebook'];
  }

  baseUrlHint(): string {
    if (this.isInstagramLoginForm()) {
      return 'Instagram Login always uses graph.instagram.com for token and API calls.';
    }
    return this.formBaseUrl.includes('instagram')
      ? 'Instagram stack — OAuth uses instagram.com and Graph API uses graph.instagram.com.'
      : 'Facebook stack — OAuth uses facebook.com and Graph API uses graph.facebook.com.';
  }

  isInstagramLoginForm(): boolean {
    return this.formPlatformCode === 'instagram_login';
  }

  appIdLabel(): string {
    return this.isInstagramLoginForm() ? 'Instagram App Id' : 'App Id';
  }

  appSecretLabel(): string {
    return this.isInstagramLoginForm() ? 'Instagram App Secret' : 'App Secret';
  }

  appIdHint(): string | null {
    if (!this.isInstagramLoginForm()) return null;
    return 'From Meta Dashboard → Instagram → API setup with Instagram login → Business login settings. Do not use the Facebook App Id from App settings → Basic.';
  }

  callbackHint(): string {
    if (this.isInstagramLoginForm()) {
      return 'Add this URL under Instagram Business login → OAuth redirect URIs (not only Facebook Login URIs).';
    }
    return 'Add this exact URL under Meta → Facebook Login → Valid OAuth Redirect URIs.';
  }

  private validateAppId(appId: string): string | null {
    const trimmed = appId.trim();
    if (/^\d{8,20}$/.test(trimmed)) return null;
    const label = this.isInstagramLoginForm() ? 'Instagram App Id' : 'App Id';
    return `${label} must be a numeric Meta app id from the Developer Dashboard (not an email or username).`;
  }

  private resolveFormScopes(): string {
    const raw = this.formScopes.trim();
    if (!this.isInstagramLoginForm()) return raw;

    const allowed = raw
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.startsWith('instagram_business_'));

    if (!allowed.some((s) => s === 'instagram_business_basic')) {
      allowed.unshift('instagram_business_basic');
    }

    return allowed.join(',');
  }

  private validateInstagramLoginScopes(): string | null {
    if (!this.isInstagramLoginForm()) return null;

    const invalid = this.formScopes
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s && !s.startsWith('instagram_business_'));

    if (!invalid.length) return null;

    return `Instagram Login cannot use Facebook scopes (${invalid.join(', ')}). Click Reset defaults and save.`;
  }

  private resolveCreatePlatformCode(): string {
    const active = this.activeCategory();
    if (active !== 'all' && PLATFORM_OPTIONS.some((p) => p.code === active)) {
      return active;
    }
    return 'facebook';
  }

  private containsInstagramBusinessScopes(scopes: string | null | undefined): boolean {
    return (scopes ?? '').toLowerCase().includes('instagram_business_');
  }

  /** instagram_business_* scopes require PlatformCode instagram_login, not instagram (Facebook Login). */
  private alignPlatformForInstagramBusinessScopes(): void {
    if (!this.containsInstagramBusinessScopes(this.formScopes)) return;
    if (this.formPlatformCode === 'instagram_login') return;

    this.formPlatformCode = 'instagram_login';
    this.formBaseUrl = this.defaultBaseUrl('instagram_login');
  }

  openEditForm(card: MetaAppConnection): void {
    this.editingId.set(card.id);
    this.formName = card.name;
    this.formDescription = card.description || '';
    this.formBaseUrl = card.baseUrl || this.defaultBaseUrl(card.platformCode);
    this.formPlatformCode = card.platformCode;
    this.formAppId = card.appId;
    this.formAppSecret = '';
    this.formCallbackUrl = card.callbackUrl;
    this.formGraphApiVersion = card.graphApiVersion || 'v21.0';
    this.formScopes = card.scopes || '';
    this.alignPlatformForInstagramBusinessScopes();
    this.formOpen.set(true);
    const dialog = this.formDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();
  }

  loadDefaultScopes(platformCode: string): void {
    this.api.getAppConnectionDefaultScopes(platformCode).subscribe({
      next: (res) => {
        if (res.success && res.data?.scopes) {
          this.formScopes = res.data.scopes;
        }
      }
    });
  }

  resetDefaultScopes(): void {
    this.loadDefaultScopes(this.formPlatformCode);
  }

  closeForm(): void {
    const dialog = this.formDialog()?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      return;
    }
    this.formOpen.set(false);
  }

  onFormClosed(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
  }

  saveForm(): void {
    if (!this.formName.trim() || !this.formAppId.trim() || !this.formCallbackUrl.trim()) {
      this.message.set('Name, App Id, and Callback URL are required.');
      return;
    }

    const appIdError = this.validateAppId(this.formAppId);
    if (appIdError) {
      this.message.set(appIdError);
      return;
    }

    const scopeError = this.validateInstagramLoginScopes();
    if (scopeError) {
      this.message.set(scopeError);
      return;
    }

    this.alignPlatformForInstagramBusinessScopes();

    const editing = this.editingId();
    if (!this.formAppSecret.trim()) {
      this.message.set('App Secret is required.');
      return;
    }

    this.savingForm.set(true);

    if (editing) {
      const body: UpdateMetaAppConnectionRequest = {
        name: this.formName.trim(),
        description: this.formDescription.trim(),
        appId: this.formAppId.trim(),
        appSecret: this.formAppSecret.trim(),
        callbackUrl: this.formCallbackUrl.trim(),
        baseUrl: this.formBaseUrl.trim(),
        graphApiVersion: this.formGraphApiVersion.trim() || 'v21.0',
        scopes: this.resolveFormScopes()
      };
      this.api.updateAppConnection(editing, body).subscribe({
        next: (res) => {
          this.savingForm.set(false);
          if (!res.success) {
            this.message.set(res.message || 'Update failed');
            return;
          }
          this.message.set(res.message || 'App connection updated.');
          this.closeForm();
          this.reload();
        },
        error: (err: { error?: { message?: string } }) => {
          this.savingForm.set(false);
          this.message.set(err?.error?.message || 'Update failed');
        }
      });
      return;
    }

    const body: CreateMetaAppConnectionRequest = {
      name: this.formName.trim(),
      description: this.formDescription.trim(),
      platformCode: this.formPlatformCode,
      appId: this.formAppId.trim(),
      appSecret: this.formAppSecret.trim(),
      callbackUrl: this.formCallbackUrl.trim(),
      baseUrl: this.formBaseUrl.trim(),
      graphApiVersion: this.formGraphApiVersion.trim() || 'v21.0',
      scopes: this.resolveFormScopes()
    };
    this.api.createAppConnection(body).subscribe({
      next: (res) => {
        this.savingForm.set(false);
        if (!res.success) {
          this.message.set(res.message || 'Create failed');
          return;
        }
        this.message.set(res.message || 'App connection created.');
        this.closeForm();
        this.reload();
      },
      error: (err: { error?: { message?: string } }) => {
        this.savingForm.set(false);
        this.message.set(err?.error?.message || 'Create failed');
      }
    });
  }

  deleteConnection(card: MetaAppConnection): void {
    if (!confirm(`Delete "${card.name}"? This removes the app configuration${card.isConnected ? ' and disconnects the linked account' : ''}.`)) {
      return;
    }

    this.api.deleteAppConnection(card.id).subscribe({
      next: () => {
        this.message.set(`${card.name} deleted.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Delete failed')
    });
  }

  async connect(card: MetaAppConnection): Promise<void> {
    const code = card.platformCode.toLowerCase();
    if (!card.canConnect || !['facebook', 'instagram', 'instagram_login', 'whatsapp'].includes(code)) {
      this.message.set(`${card.platformName} is coming soon.`);
      return;
    }

    if (!card.appId?.trim()) {
      this.message.set('Configure App Id before connecting.');
      return;
    }

    const appIdError = this.validateAppId(card.appId);
    if (appIdError) {
      this.message.set(appIdError);
      return;
    }

    this.connecting.set(card.id);
    this.message.set(
      code === 'instagram_login'
        ? `Opening Instagram Login for ${card.platformName}…`
        : `Opening Meta login for ${card.platformName}…`
    );

    try {
      const result = await this.appAuth.openPopup(card.id);
      if (!result.ok) {
        this.message.set(result.message || 'Connection failed');
        return;
      }

      const list = await this.reloadAsync();
      const refreshed = list.find((c) => c.id === card.id) ?? card;

      if (this.supportsPageSelection(refreshed.platformCode)) {
        this.message.set(`Signed in with Meta. Choose the ${refreshed.platformName} page you want to manage.`);
        this.openPagePicker(refreshed, true);
      } else {
        this.message.set(`${refreshed.platformName} connected.`);
      }
    } catch (err) {
      this.message.set(err instanceof Error ? err.message : 'Connection failed');
    } finally {
      this.connecting.set(null);
    }
  }

  supportsPageSelection(code: string): boolean {
    return supportsIntegrationPageSelection(code);
  }

  supportsConnectionDetails(code: string): boolean {
    return supportsIntegrationConnectionDetails(code);
  }

  isConnecting(card: IntegrationCardView): boolean {
    return this.connecting() === card.connectingKey;
  }

  /** True when OAuth finished and a page/account is fully linked. */
  isPageLinked(card: IntegrationCardView): boolean {
    return card.isConnected && !card.requiresPageSelection;
  }

  tone(code: string): string {
    return integrationTone(code);
  }

  shortDesc(text: string): string {
    return integrationShortDesc(text);
  }

  platformIcon(code: string): string {
    return integrationPlatformIcon(code);
  }

  isInstagramLoginPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'instagram_login';
  }

  isInstagramPlatform(code: string | null | undefined): boolean {
    const value = (code || '').toLowerCase();
    return value === 'instagram' || value === 'instagram_login';
  }

  openPagePicker(card: MetaAppConnection, retryOnEmpty = false): void {
    this.pickerConnection.set(card);
    void this.loadPagesAsync(retryOnEmpty);

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
    this.pickerConnection.set(null);
    this.pages.set([]);
    this.pagesError.set('');
    this.selectedPageId.set(null);
  }

  loadPages(): void {
    void this.loadPagesAsync(false);
  }

  private async loadPagesAsync(retryOnEmpty: boolean): Promise<void> {
    const card = this.pickerConnection();
    if (!card) return;

    this.pagesLoading.set(true);
    this.pagesError.set('');

    const fetchPages = async (): Promise<MetaPage[]> => {
      const res = await firstValueFrom(this.api.getAppConnectionPages(card.id));
      if (!res.success) {
        throw new Error(res.message || 'Could not load your pages.');
      }
      return res.data || [];
    };

    try {
      let list = await fetchPages();
      if (list.length === 0 && retryOnEmpty) {
        await new Promise((resolve) => setTimeout(resolve, 800));
        list = await fetchPages();
      }

      this.pages.set(list);
      this.selectedPageId.set(list.find((p) => p.isSelected && p.isEligible)?.pageId || null);

      if (list.length === 0) {
        const hint =
          'No Facebook Pages came back for this login. Add business_management to scopes (required for Business Manager pages), include pages_show_list, then disconnect and reconnect.';
        this.pagesError.set(hint);
        this.message.set(hint);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Could not load your pages.';
      this.pagesError.set(msg);
      this.message.set(msg);
    } finally {
      this.pagesLoading.set(false);
    }
  }

  togglePage(page: MetaPage): void {
    if (!page.isEligible) return;
    this.selectedPageId.update((current) => (current === page.pageId ? null : page.pageId));
  }

  isPageChecked(page: MetaPage): boolean {
    return this.selectedPageId() === page.pageId;
  }

  confirmPage(): void {
    void this.confirmPageAsync();
  }

  private async confirmPageAsync(): Promise<void> {
    const card = this.pickerConnection();
    const pageId = this.selectedPageId();
    if (!card || !pageId) {
      this.message.set('Select a page before continuing.');
      return;
    }

    this.savingPage.set(true);
    this.pagesError.set('');

    try {
      const res = await firstValueFrom(this.api.selectAppConnectionPage(card.id, pageId));
      if (!res.success) {
        const err = res.message || 'Could not connect that page.';
        this.pagesError.set(err);
        this.message.set(err);
        return;
      }

      await this.reloadAsync();
      this.message.set(res.message || 'Page connected.');
      this.closePagePicker();
    } catch (err: unknown) {
      const msg =
        (err as { error?: { message?: string } })?.error?.message || 'Could not connect that page.';
      this.pagesError.set(msg);
      this.message.set(msg);
    } finally {
      this.savingPage.set(false);
    }
  }

  openDetails(card: MetaAppConnection): void {
    this.detailsTitle.set(card.platformName);
    this.details.set(null);
    this.detailsError.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
    this.detailsLoading.set(true);
    this.detailsOpen.set(true);

    const dialog = this.detailsDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();

    this.api.getAppConnectionDetails(card.id).subscribe({
      next: (res: ApiResponse<AppConnectionDetails>) => {
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
    this.details.set(null);
    this.detailsError.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
  }

  tokenLabel(platformCode: string): string {
    const code = (platformCode || '').toLowerCase();
    if (code === 'facebook' || code === 'instagram') return 'Page access token';
    if (code === 'instagram_login') return 'Instagram access token';
    return 'Access token';
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

  disconnect(card: MetaAppConnection): void {
    this.api.disconnectAppConnection(card.id).subscribe({
      next: () => {
        this.message.set(`${card.platformName} disconnected.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Disconnect failed')
    });
  }
}
