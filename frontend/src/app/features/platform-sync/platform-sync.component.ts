import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs/operators';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { ProcessMenuType } from '../../core/config/process.config';
import {
  ApiResponse,
  ConnectionDetails,
  PlatformCard,
  PlatformSyncResult
} from '../../core/models/api.models';

type SyncAction = 'posts' | 'comments' | 'statistics';

interface ManualSyncPlatform {
  card: PlatformCard;
  tone: 'youtube' | 'tiktok';
  icon: string;
  actions: SyncAction[];
  hint: string;
}

const MANUAL_SYNC_CONFIG: Record<string, Omit<ManualSyncPlatform, 'card'>> = {
  youtube: {
    tone: 'youtube',
    icon: 'smart_display',
    actions: ['posts', 'comments', 'statistics'],
    hint: 'Fetch channel videos, comments, and engagement stats on demand.'
  },
  tiktok: {
    tone: 'tiktok',
    icon: 'music_video',
    actions: ['posts', 'statistics'],
    hint: 'Fetch published TikTok videos and refresh view, like, and share counts.'
  }
};

@Component({
  selector: 'app-platform-sync',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './platform-sync.component.html',
  styleUrl: './platform-sync.component.scss'
})
export class PlatformSyncComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly detailsDialog = viewChild<ElementRef<HTMLDialogElement>>('detailsDialog');

  readonly cards = signal<PlatformCard[]>([]);
  readonly loading = signal(true);
  readonly message = signal('');
  readonly lastResult = signal<PlatformSyncResult | null>(null);
  readonly lastResultPlatform = signal('');
  readonly running = signal<{ platform: string; action: SyncAction } | null>(null);

  readonly detailsOpen = signal(false);
  readonly detailsLoading = signal(false);
  readonly detailsError = signal('');
  readonly detailsTitle = signal('');
  readonly detailsPlatform = signal('');
  readonly details = signal<ConnectionDetails | null>(null);
  readonly tokenRevealed = signal(false);
  readonly tokenCopied = signal(false);

  readonly connectLink = computed(() => `${this.processRoute.currentRouteBase()}/connect`);

  readonly manualPlatforms = computed<ManualSyncPlatform[]>(() =>
    this.cards()
      .filter((card) => card.code.toLowerCase() in MANUAL_SYNC_CONFIG)
      .map((card) => {
        const config = MANUAL_SYNC_CONFIG[card.code.toLowerCase()];
        return { card, ...config };
      })
  );

  readonly connectedCount = computed(
    () => this.manualPlatforms().filter((entry) => entry.card.isConnected).length
  );

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.processApi
      .getPlatforms(this.processRoute.currentMenuType())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res: ApiResponse<PlatformCard[]>) => this.cards.set(res.data || []),
        error: () => this.message.set('Could not load platforms for manual sync.')
      });
  }

  isRunning(code: string, action: SyncAction): boolean {
    const run = this.running();
    return !!run && run.platform === code.toLowerCase() && run.action === action;
  }

  actionLabel(action: SyncAction, code: string): string {
    if (this.isRunning(code, action)) {
      return action === 'posts'
        ? 'Fetching posts…'
        : action === 'comments'
          ? 'Fetching comments…'
          : 'Refreshing stats…';
    }

    return action === 'posts'
      ? 'Fetch posts'
      : action === 'comments'
        ? 'Fetch comments'
        : 'Fetch statistics';
  }

  actionIcon(action: SyncAction): string {
    return action === 'posts' ? 'video_library' : action === 'comments' ? 'mode_comment' : 'insights';
  }

  runSync(card: PlatformCard, action: SyncAction): void {
    const code = card.code.toLowerCase();
    if (!card.isConnected) {
      this.message.set(`Connect ${card.displayName} before fetching data.`);
      return;
    }

    this.running.set({ platform: code, action });
    this.message.set('');
    this.lastResult.set(null);
    this.lastResultPlatform.set('');

    const menuType = this.processRoute.currentMenuType();
    const request = this.resolveSyncRequest(menuType, code, action);

    request.pipe(finalize(() => this.running.set(null))).subscribe({
      next: (res) => {
        if (!res.success || !res.data) {
          this.message.set(res.message || 'Sync failed.');
          return;
        }
        this.lastResult.set(res.data);
        this.lastResultPlatform.set(card.displayName);
        this.message.set(res.message || this.describeResult(res.data, action));
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Sync failed.')
    });
  }

  openDetails(card: PlatformCard): void {
    this.detailsTitle.set(card.displayName);
    this.detailsPlatform.set(card.code.toLowerCase());
    this.details.set(null);
    this.detailsError.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
    this.detailsLoading.set(true);
    this.detailsOpen.set(true);

    const dialog = this.detailsDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();

    this.processApi.getConnectionDetails(this.processRoute.currentMenuType(), card.code).subscribe({
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

  disconnect(card: PlatformCard): void {
    this.processApi.disconnect(this.processRoute.currentMenuType(), card.code).subscribe({
      next: () => {
        this.message.set(`${card.displayName} disconnected.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Disconnect failed')
    });
  }

  profileName(info: ConnectionDetails): string {
    return info.pageName || info.profiles?.[0]?.name || info.accountName || '—';
  }

  profileId(info: ConnectionDetails): string {
    return info.pageId || info.profiles?.[0]?.externalProfileId || '—';
  }

  isTikTokDetails(): boolean {
    return this.detailsPlatform() === 'tiktok';
  }

  tone(code: string): string {
    const map: Record<string, string> = {
      youtube: 'youtube',
      tiktok: 'tiktok'
    };
    return map[code.toLowerCase()] || 'slate';
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

  private resolveSyncRequest(menuType: ProcessMenuType, code: string, action: SyncAction) {
    if (code === 'tiktok') {
      return action === 'posts'
        ? this.processApi.syncTikTokPosts(menuType, code)
        : this.processApi.syncTikTokStatistics(menuType, code);
    }

    return action === 'posts'
      ? this.processApi.syncYouTubePosts(menuType, code)
      : action === 'comments'
        ? this.processApi.syncYouTubeComments(menuType, code)
        : this.processApi.syncYouTubeStatistics(menuType, code);
  }

  private resetDetails(): void {
    this.detailsOpen.set(false);
    this.details.set(null);
    this.detailsError.set('');
    this.detailsPlatform.set('');
    this.tokenRevealed.set(false);
    this.tokenCopied.set(false);
  }

  private describeResult(result: PlatformSyncResult, action: SyncAction): string {
    const label =
      action === 'posts' ? 'videos' : action === 'comments' ? 'comments' : 'statistics';
    return `Fetched ${result.fetched} ${label}, stored ${result.stored}, updated ${result.updated}.`;
  }
}
