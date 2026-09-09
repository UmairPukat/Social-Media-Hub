import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';
import { MetaAdAccount } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-ad-accounts',
  standalone: true,
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './meta-ads-ad-accounts.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsAdAccountsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  private readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly accounts = signal<MetaAdAccount[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly selectedId = signal<string | null>(null);

  readonly processLabel = () =>
    PROCESS_MODULE_LIST.find(m => m.id === this.processRoute.currentMenuType())?.label ?? 'Process';

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    this.selectedId.set(this.state.selectedAdAccount()?.id ?? null);
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
        this.accounts.set(res.data ?? []);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load ad accounts.');
      }
    });
  }

  select(account: MetaAdAccount): void {
    this.selectedId.set(account.id);
    this.state.selectAdAccount({ id: account.id, name: account.name, currency: account.currency });
    this.banner.set(`Selected ad account: ${account.name} (${account.id})`);
  }

  isSelected(account: MetaAdAccount): boolean {
    return this.selectedId() === account.id;
  }
}
