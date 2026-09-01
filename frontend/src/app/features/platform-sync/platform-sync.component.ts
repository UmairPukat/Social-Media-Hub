import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs/operators';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import {
  ApiResponse,
  PlatformCard,
  YouTubeSyncResult
} from '../../core/models/api.models';

type SyncAction = 'posts' | 'comments' | 'statistics';

interface ManualSyncPlatform {
  card: PlatformCard;
  tone: string;
  icon: string;
}

@Component({
  selector: 'app-platform-sync',
  standalone: true,
  imports: [DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './platform-sync.component.html',
  styleUrl: './platform-sync.component.scss'
})
export class PlatformSyncComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly cards = signal<PlatformCard[]>([]);
  readonly loading = signal(true);
  readonly message = signal('');
  readonly lastResult = signal<YouTubeSyncResult | null>(null);
  readonly running = signal<{ platform: string; action: SyncAction } | null>(null);

  readonly connectLink = computed(() => `${this.processRoute.currentRouteBase()}/connect`);

  /** Platforms without webhooks that expose manual fetch actions. */
  readonly manualPlatforms = computed<ManualSyncPlatform[]>(() =>
    this.cards()
      .filter((card) => card.code.toLowerCase() === 'youtube')
      .map((card) => ({
        card,
        tone: 'rose',
        icon: 'smart_display'
      }))
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

  runSync(card: PlatformCard, action: SyncAction): void {
    const code = card.code.toLowerCase();
    if (!card.isConnected) {
      this.message.set(`Connect ${card.displayName} before fetching data.`);
      return;
    }

    this.running.set({ platform: code, action });
    this.message.set('');
    this.lastResult.set(null);

    const menuType = this.processRoute.currentMenuType();
    const request =
      action === 'posts'
        ? this.processApi.syncYouTubePosts(menuType, code)
        : action === 'comments'
          ? this.processApi.syncYouTubeComments(menuType, code)
          : this.processApi.syncYouTubeStatistics(menuType, code);

    request.pipe(finalize(() => this.running.set(null))).subscribe({
      next: (res) => {
        if (!res.success || !res.data) {
          this.message.set(res.message || 'Sync failed.');
          return;
        }
        this.lastResult.set(res.data);
        this.message.set(res.message || this.describeResult(res.data, action));
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Sync failed.')
    });
  }

  private describeResult(result: YouTubeSyncResult, action: SyncAction): string {
    const label =
      action === 'posts' ? 'videos' : action === 'comments' ? 'comments' : 'statistics';
    return `Fetched ${result.fetched} ${label}, stored ${result.stored}, updated ${result.updated}.`;
  }
}
