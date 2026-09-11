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
import { MetaAd, MetaAdSet } from '../../core/models/meta-ads.models';
import { metaAdsManagerAdSetUrl, metaAdsManagerAdUrl, metaAdsManagerCampaignUrl, metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-ads',
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  templateUrl: './meta-ads-ads.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsAdsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly route = inject(ActivatedRoute);

  readonly adSets = signal<MetaAdSet[]>([]);
  readonly ads = signal<MetaAd[]>([]);
  readonly selectedAdSetId = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.ads().filter(a => !term || a.name.toLowerCase().includes(term) || a.id.includes(term));
  });

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    const adSetId = this.route.snapshot.queryParamMap.get('adSetId');
    if (adSetId) {
      this.selectedAdSetId.set(adSetId);
    }
    this.loadAdSets();
    this.loadAds();
  }

  loadAdSets(): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) return;

    this.api.getAdSets(this.processRoute.currentMenuType(), { adAccountId: adAccount.id, limit: 50 }).subscribe({
      next: (res) => {
        if (res.success) {
          this.adSets.set(res.data?.items ?? []);
          const requested = this.route.snapshot.queryParamMap.get('adSetId');
          if (requested) {
            this.selectedAdSetId.set(requested);
          } else if (!this.selectedAdSetId() && this.adSets().length) {
            this.selectedAdSetId.set(this.adSets()[0].id);
          }
        }
      }
    });
  }

  loadAds(): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) {
      this.ads.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api
      .getAds(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        adSetId: this.selectedAdSetId() || undefined,
        limit: 25
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.ads.set(res.data?.items ?? []);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Unable to load ads.');
        }
      });
  }

  metaManagerUrl(ad: MetaAd): string {
    const accountId = this.state.selectedAdAccount()?.id ?? '';
    return metaAdsManagerAdUrl(accountId, ad.id);
  }

  metaAdSetUrl(adSetId: string): string {
    const accountId = this.state.selectedAdAccount()?.id ?? '';
    return metaAdsManagerAdSetUrl(accountId, adSetId);
  }

  metaCampaignUrl(campaignId: string): string {
    const accountId = this.state.selectedAdAccount()?.id ?? '';
    return metaAdsManagerCampaignUrl(accountId, campaignId);
  }

  updateStatus(ad: MetaAd, status: string): void {
    this.api.updateAd(this.processRoute.currentMenuType(), ad.id, { status }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set(`Ad ${ad.name} is now ${status}.`);
        this.loadAds();
      },
      error: () => this.error.set('Unable to update ad.')
    });
  }
}
