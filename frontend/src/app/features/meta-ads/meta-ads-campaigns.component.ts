import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { filter } from 'rxjs/operators';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaCampaign } from '../../core/models/meta-ads.models';
import { metaAdsManagerCampaignUrl, metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-campaigns',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './meta-ads-campaigns.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsCampaignsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly campaigns = signal<MetaCampaign[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');
  readonly status = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly cursorStack = signal<string[]>([]);
  readonly includeCampaignId = signal<string | null>(null);

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const statusFilter = this.status().trim().toUpperCase();
    return this.campaigns().filter(c => {
      if (statusFilter) {
        const campaignStatus = (c.status || '').toUpperCase();
        const effectiveStatus = (c.effectiveStatus || '').toUpperCase();
        if (campaignStatus !== statusFilter && effectiveStatus !== statusFilter) {
          return false;
        }
      }
      if (!term) return true;
      return c.name.toLowerCase().includes(term) || c.id.includes(term);
    });
  });

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        if (!this.router.url.includes('/meta-ads/campaigns')) return;
        this.state.syncForCurrentProcess();
        this.readCreatedQueryParam();
        this.refresh();
      });
  }

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    this.readCreatedQueryParam();
    this.refresh();
  }

  load(after?: string): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) {
      this.campaigns.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api
      .getCampaigns(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        after,
        includeCampaignId: this.includeCampaignId() || undefined,
        limit: 100
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.campaigns.set(res.data?.items ?? []);
          this.nextCursor.set(res.data?.nextCursor ?? null);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Unable to load campaigns.');
        }
      });
  }

  refresh(): void {
    this.cursorStack.set([]);
    this.load();
  }

  nextPage(): void {
    const cursor = this.nextCursor();
    if (!cursor) return;
    this.cursorStack.update(stack => [...stack, cursor]);
    this.load(cursor);
  }

  prevPage(): void {
    const stack = [...this.cursorStack()];
    stack.pop();
    const prev = stack.at(-1);
    this.cursorStack.set(stack);
    this.load(prev);
  }

  pause(campaign: MetaCampaign): void {
    this.updateStatus(campaign, 'PAUSED');
  }

  resume(campaign: MetaCampaign): void {
    this.updateStatus(campaign, 'ACTIVE');
  }

  private readCreatedQueryParam(): void {
    const created = this.route.snapshot.queryParamMap.get('created');
    if (!created) return;

    this.includeCampaignId.set(created);
    this.banner.set(`Campaign created successfully. Showing campaign ID ${created}.`);
    this.status.set('');
    this.search.set('');

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { created: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  metaManagerUrl(campaign: MetaCampaign): string {
    const accountId = this.state.selectedAdAccount()?.id ?? '';
    return metaAdsManagerCampaignUrl(accountId, campaign.id);
  }

  private updateStatus(campaign: MetaCampaign, status: string): void {
    this.api.updateCampaign(this.processRoute.currentMenuType(), campaign.id, { status }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set(`Campaign ${campaign.name} is now ${status}.`);
        this.refresh();
      },
      error: () => this.error.set('Unable to update campaign.')
    });
  }
}
