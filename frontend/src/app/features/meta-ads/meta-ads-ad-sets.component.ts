import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaAdSet, MetaCampaign } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-ad-sets',
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
  templateUrl: './meta-ads-ad-sets.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsAdSetsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly route = inject(ActivatedRoute);

  readonly campaigns = signal<MetaCampaign[]>([]);
  readonly adSets = signal<MetaAdSet[]>([]);
  readonly selectedCampaignId = signal('');
  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');
  readonly nextCursor = signal<string | null>(null);

  readonly newName = signal('App Review Demo Ad Set');
  readonly newDailyBudget = signal(1000);

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.adSets().filter(a => !term || a.name.toLowerCase().includes(term) || a.id.includes(term));
  });

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    const campaignId = this.route.snapshot.queryParamMap.get('campaignId');
    if (campaignId) {
      this.selectedCampaignId.set(campaignId);
    }
    this.loadCampaigns();
    this.loadAdSets();
  }

  loadCampaigns(): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) return;

    this.api.getCampaigns(this.processRoute.currentMenuType(), { adAccountId: adAccount.id, limit: 50 }).subscribe({
      next: (res) => {
        if (res.success) {
          this.campaigns.set(res.data?.items ?? []);
          const requested = this.route.snapshot.queryParamMap.get('campaignId');
          if (requested) {
            this.selectedCampaignId.set(requested);
          } else if (!this.selectedCampaignId() && this.campaigns().length) {
            this.selectedCampaignId.set(this.campaigns()[0].id);
          }
        }
      }
    });
  }

  loadAdSets(after?: string): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) {
      this.adSets.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api
      .getAdSets(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        campaignId: this.selectedCampaignId() || undefined,
        after,
        limit: 25
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.adSets.set(res.data?.items ?? []);
          this.nextCursor.set(res.data?.nextCursor ?? null);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Unable to load ad sets.');
        }
      });
  }

  createAdSet(): void {
    const adAccount = this.state.selectedAdAccount();
    const campaignId = this.selectedCampaignId();
    if (!adAccount || !campaignId) return;

    this.creating.set(true);
    this.api
      .createAdSet(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        campaignId,
        name: this.newName().trim(),
        status: 'PAUSED',
        dailyBudget: this.newDailyBudget()
      })
      .subscribe({
        next: (res) => {
          this.creating.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.banner.set(`Ad set created (PAUSED). ID: ${res.data?.id}`);
          this.loadAdSets();
        },
        error: () => {
          this.creating.set(false);
          this.error.set('Unable to create ad set.');
        }
      });
  }

  updateStatus(adSet: MetaAdSet, status: string): void {
    this.api.updateAdSet(this.processRoute.currentMenuType(), adSet.id, { status }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set(`Ad set ${adSet.name} is now ${status}.`);
        this.loadAdSets();
      },
      error: () => this.error.set('Unable to update ad set.')
    });
  }
}
