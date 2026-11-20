import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
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
import { MetaAd, MetaAdSet, MetaPageOption } from '../../core/models/meta-ads.models';
import { watchMetaAdsMenu } from './meta-ads-menu.watch';
import { metaAdsManagerAdSetUrl, metaAdsManagerAdUrl, metaAdsManagerCampaignUrl, metaAdsModuleLabel, metaErrorMessage } from './meta-ads.util';

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
  private readonly sanitizer = inject(DomSanitizer);
  readonly moduleLabel = () => metaAdsModuleLabel(this.processRoute.currentMenuType());

  readonly adSets = signal<MetaAdSet[]>([]);
  readonly ads = signal<MetaAd[]>([]);
  readonly selectedAdSetId = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');
  readonly pages = signal<MetaPageOption[]>([]);
  readonly pageId = signal('');
  readonly adName = signal('App Review Paused Ad');
  readonly adMessage = signal('SocialHub app review sample');
  readonly adLink = signal('https://example.com');
  readonly previewHtml = signal<SafeHtml | null>(null);
  readonly previewFor = signal('');

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.ads().filter(a => !term || a.name.toLowerCase().includes(term) || a.id.includes(term));
  });

  constructor() {
    watchMetaAdsMenu(() => this.reloadForMenu());
  }

  ngOnInit(): void {
    this.reloadForMenu();
  }

  private reloadForMenu(): void {
    this.state.syncForCurrentProcess();
    this.adSets.set([]);
    this.ads.set([]);
    this.banner.set('');
    this.error.set('');
    const adSetId = this.route.snapshot.queryParamMap.get('adSetId');
    this.selectedAdSetId.set(adSetId ?? '');
    this.loading.set(true);
    this.state.ensureAdAccount().subscribe({
      next: (result) => {
        if (result.error || !result.account) {
          this.loading.set(false);
          if (result.error) this.error.set(result.error);
          return;
        }
        this.loadPages();
        this.loadAdSets();
        this.loadAds();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load ad accounts.');
      }
    });
  }

  loadPages(): void {
    this.api.getPages(this.processRoute.currentMenuType()).subscribe({
      next: (res) => {
        if (!res.success) return;
        const pages = res.data ?? [];
        this.pages.set(pages);
        const saved = this.state.selectedPage()?.id;
        const match = pages.find(page => page.id === saved) ?? pages[0];
        if (match) {
          this.pageId.set(match.id);
          this.state.selectPage({ id: match.id, name: match.name });
        }
      }
    });
  }

  onPageChange(pageId: string): void {
    this.pageId.set(pageId);
    const page = this.pages().find(item => item.id === pageId);
    if (page) this.state.selectPage({ id: page.id, name: page.name });
  }

  pageName(pageId?: string): string {
    if (!pageId) return '—';
    return this.pages().find(page => page.id === pageId)?.name ?? pageId;
  }

  createAd(): void {
    const adAccount = this.state.selectedAdAccount();
    const adSetId = this.selectedAdSetId();
    if (!adAccount || !adSetId || !this.pageId()) {
      this.error.set('Select an ad account, ad set, and Facebook Page before creating an ad.');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api.createAd(this.processRoute.currentMenuType(), {
      adAccountId: adAccount.id,
      adSetId,
      name: this.adName().trim(),
      pageId: this.pageId(),
      link: this.adLink().trim(),
      message: this.adMessage().trim()
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        const pageLabel = this.pageName(res.data.pageId);
        this.banner.set(`Paused ad ${res.data.id} uses Facebook Page ${pageLabel}.`);
        this.loadAds();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to create the ad.');
      }
    });
  }

  preview(ad: MetaAd): void {
    this.previewFor.set(ad.id);
    this.previewHtml.set(null);
    this.api.getAdPreview(this.processRoute.currentMenuType(), ad.id).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.previewHtml.set(res.data?.html ? this.sanitizer.bypassSecurityTrustHtml(res.data.html) : null);
      },
      error: () => this.error.set('Unable to load the ad preview.')
    });
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
