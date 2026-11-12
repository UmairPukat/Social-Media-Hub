import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { MetaRateLimitService } from '../../core/services/meta-rate-limit.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';
import { MetaAdAccount } from '../../core/models/meta-ads.models';
import { watchMetaAdsMenu } from './meta-ads-menu.watch';
import { metaAdsManagerAdAccountUrl, metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-ad-accounts',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './meta-ads-ad-accounts.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsAdAccountsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  private readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  readonly rateLimit = inject(MetaRateLimitService);

  readonly accounts = signal<MetaAdAccount[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly selectedId = signal<string | null>(null);

  readonly usagePercent = computed(() =>
    Math.max(
      this.rateLimit.bucUsagePercent(),
      this.rateLimit.adAccountUsagePercent(),
      this.rateLimit.appUsagePercent()
    )
  );

  readonly processLabel = () =>
    PROCESS_MODULE_LIST.find(m => m.id === this.processRoute.currentMenuType())?.label ?? 'Process';

  constructor() {
    watchMetaAdsMenu(() => this.reloadForMenu());
  }

  ngOnInit(): void {
    this.reloadForMenu();
  }

  private reloadForMenu(): void {
    this.state.syncForCurrentProcess();
    this.selectedId.set(this.state.selectedAdAccount()?.id ?? null);
    this.banner.set('');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getAdAccounts(this.processRoute.currentMenuType()).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        const rows = res.data ?? [];
        this.accounts.set(rows);
        this.keepOrSelectAccount(rows);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load ad accounts.');
      }
    });
  }

  private keepOrSelectAccount(rows: MetaAdAccount[]): void {
    const current = this.state.selectedAdAccount();
    if (current && rows.some(account => account.id === current.id)) {
      this.selectedId.set(current.id);
      return;
    }
    const selected = this.state.selectBestAccount(rows);
    this.selectedId.set(selected?.id ?? null);
    if (selected) this.banner.set(`Selected ad account: ${selected.name} (${selected.id})`);
  }

  syncAdAccounts(): void {
    this.banner.set('Syncing ad accounts from Meta…');
    this.load();
  }

  select(account: MetaAdAccount): void {
    this.selectedId.set(account.id);
    this.state.selectAdAccount({
      id: account.id,
      name: account.name,
      currency: account.currency
    });
    this.banner.set(`Selected ad account: ${account.name} (${account.id})`);
  }

  isSelected(account: MetaAdAccount): boolean {
    return this.selectedId() === account.id;
  }

  metaManagerUrl(account: MetaAdAccount): string {
    return metaAdsManagerAdAccountUrl(account.id);
  }
}
