import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
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
import { IntegrationPlatformCardComponent } from '../../shared/integration-platform-card/integration-platform-card.component';
import {
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

@Component({
  selector: 'app-app-connections',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    IntegrationPlatformCardComponent
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
  formPlatformCode = 'facebook';
  formAppId = '';
  formAppSecret = '';
  formCallbackUrl = this.defaultCallbackUrl;
  formGraphApiVersion = 'v21.0';
  formScopes = '';

  readonly connectedCount = computed(() => this.connections().filter((c) => c.isConnected).length);
  readonly eligiblePages = computed(() => this.pages().filter((p) => p.isEligible));
  readonly platformOptions = PLATFORM_OPTIONS;

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
      displayName: conn.platformName,
      description: conn.name,
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
    this.formPlatformCode = 'facebook';
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

  openEditForm(card: MetaAppConnection): void {
    this.editingId.set(card.id);
    this.formName = card.name;
    this.formPlatformCode = card.platformCode;
    this.formAppId = card.appId;
    this.formAppSecret = '';
    this.formCallbackUrl = card.callbackUrl;
    this.formGraphApiVersion = card.graphApiVersion || 'v21.0';
    this.formScopes = card.scopes || '';
    this.formOpen.set(true);
    const dialog = this.formDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();
  }

  onPlatformChange(code: string): void {
    this.formPlatformCode = code;
    this.loadDefaultScopes(code);
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

    const editing = this.editingId();
    if (!this.formAppSecret.trim()) {
      this.message.set('App Secret is required.');
      return;
    }

    this.savingForm.set(true);

    if (editing) {
      const body: UpdateMetaAppConnectionRequest = {
        name: this.formName.trim(),
        appId: this.formAppId.trim(),
        appSecret: this.formAppSecret.trim(),
        callbackUrl: this.formCallbackUrl.trim(),
        graphApiVersion: this.formGraphApiVersion.trim() || 'v21.0',
        scopes: this.formScopes.trim()
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
      platformCode: this.formPlatformCode,
      appId: this.formAppId.trim(),
      appSecret: this.formAppSecret.trim(),
      callbackUrl: this.formCallbackUrl.trim(),
      graphApiVersion: this.formGraphApiVersion.trim() || 'v21.0',
      scopes: this.formScopes.trim()
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
    if (!card.appId?.trim()) {
      this.message.set('Configure App Id before connecting.');
      return;
    }

    this.connecting.set(card.id);
    this.message.set(`Opening Meta login for ${card.platformName}…`);

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
        this.openPagePicker(refreshed);
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

  isInstagramLoginPlatform(code: string | null | undefined): boolean {
    return (code || '').toLowerCase() === 'instagram_login';
  }

  isInstagramPlatform(code: string | null | undefined): boolean {
    const value = (code || '').toLowerCase();
    return value === 'instagram' || value === 'instagram_login';
  }

  openPagePicker(card: MetaAppConnection): void {
    this.pickerConnection.set(card);
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
    this.pickerConnection.set(null);
    this.pages.set([]);
    this.pagesError.set('');
    this.selectedPageId.set(null);
  }

  loadPages(): void {
    const card = this.pickerConnection();
    if (!card) return;

    this.pagesLoading.set(true);
    this.pagesError.set('');
    this.api.getAppConnectionPages(card.id).subscribe({
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
    const card = this.pickerConnection();
    const pageId = this.selectedPageId();
    if (!card || !pageId) return;

    this.savingPage.set(true);
    this.api.selectAppConnectionPage(card.id, pageId).subscribe({
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
