import { DecimalPipe } from '@angular/common';
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
import { MetaAd, MetaAdSet, MetaCampaign, MetaInsightsSummary } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

type InsightLevel = 'campaign' | 'adset' | 'ad';
type DatePreset = 'today' | 'yesterday' | 'last_7d' | 'last_30d' | 'custom';

interface ChartPoint {
  x: number;
  y: number;
  label: string;
  value: number;
}

@Component({
  selector: 'app-meta-ads-insights',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './meta-ads-insights.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsInsightsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly route = inject(ActivatedRoute);

  readonly level = signal<InsightLevel>('campaign');
  readonly campaigns = signal<MetaCampaign[]>([]);
  readonly adSets = signal<MetaAdSet[]>([]);
  readonly ads = signal<MetaAd[]>([]);
  readonly selectedObjectId = signal('');
  readonly datePreset = signal<DatePreset>('last_7d');
  readonly since = signal('');
  readonly until = signal('');
  readonly summary = signal<MetaInsightsSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  readonly chartPoints = computed<ChartPoint[]>(() => {
    const rows = this.summary()?.rows ?? [];
    if (!rows.length) return [];
    const values = rows.map(r => Number(r.impressions || 0));
    const max = Math.max(...values, 1) * 1.1;
    const width = 760;
    const height = 200;
    return rows.map((row, index) => {
      const value = Number(row.impressions || 0);
      return {
        x: rows.length === 1 ? 0 : (index / (rows.length - 1)) * width,
        y: height - (value / max) * height,
        label: row.dateStart || `#${index + 1}`,
        value
      };
    });
  });

  readonly linePath = computed(() =>
    this.chartPoints().map((p, i) => `${i ? 'L' : 'M'} ${p.x} ${p.y}`).join(' ')
  );

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    const campaignId = this.route.snapshot.queryParamMap.get('campaignId');
    const level = this.route.snapshot.queryParamMap.get('level');
    if (level === 'campaign' || level === 'adset' || level === 'ad') {
      this.level.set(level);
    }
    if (campaignId) {
      this.level.set('campaign');
      this.selectedObjectId.set(campaignId);
    }
    this.loadObjects(campaignId ?? undefined);
  }

  loadObjects(preferredCampaignId?: string): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) return;

    const menu = this.processRoute.currentMenuType();
    this.api.getCampaigns(menu, { adAccountId: adAccount.id, limit: 50, includeCampaignId: preferredCampaignId }).subscribe({
      next: (res) => {
        if (res.success) {
          this.campaigns.set(res.data?.items ?? []);
          if (preferredCampaignId) {
            this.selectedObjectId.set(preferredCampaignId);
          } else if (!this.selectedObjectId() && this.campaigns().length) {
            this.selectedObjectId.set(this.campaigns()[0].id);
          }
          if (preferredCampaignId && this.level() === 'campaign') {
            this.loadInsights();
          }
        }
      }
    });

    this.api.getAdSets(menu, { adAccountId: adAccount.id, limit: 50 }).subscribe({
      next: (res) => {
        if (res.success) this.adSets.set(res.data?.items ?? []);
      }
    });

    this.api.getAds(menu, { adAccountId: adAccount.id, limit: 50 }).subscribe({
      next: (res) => {
        if (res.success) this.ads.set(res.data?.items ?? []);
      }
    });
  }

  onLevelChange(): void {
    const level = this.level();
    const first =
      level === 'campaign' ? this.campaigns()[0]?.id :
      level === 'adset' ? this.adSets()[0]?.id :
      this.ads()[0]?.id;
    this.selectedObjectId.set(first || '');
  }

  loadInsights(): void {
    const objectId = this.selectedObjectId().trim();
    if (!objectId) {
      this.error.set('Select an object to load insights.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    const preset = this.datePreset();
    const query = {
      objectId,
      level: this.level(),
      datePreset: preset === 'custom' ? undefined : preset,
      since: preset === 'custom' ? this.since() : undefined,
      until: preset === 'custom' ? this.until() : undefined
    };

    this.api.getInsights(this.processRoute.currentMenuType(), query).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          this.summary.set(null);
          return;
        }
        this.summary.set(res.data);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load insights.');
      }
    });
  }
}
