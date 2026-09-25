import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { MetaRateLimitService } from '../../core/services/meta-rate-limit.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaInsightRow, MetaInsightsSummary } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

type InsightLevel = 'account' | 'campaign' | 'adset' | 'ad';
type DatePreset = 'last_7d' | 'last_30d';

const FIELD_OPTIONS = [
  { id: 'spend', label: 'Spend' },
  { id: 'impressions', label: 'Impressions' },
  { id: 'clicks', label: 'Clicks' },
  { id: 'ctr', label: 'CTR' },
  { id: 'cpm', label: 'CPM' },
  { id: 'cpp', label: 'CPP' },
  { id: 'actions', label: 'Actions' },
  { id: 'purchase_roas', label: 'Purchase ROAS' }
] as const;

@Component({
  selector: 'app-meta-ads-insights',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule
  ],
  templateUrl: './meta-ads-insights.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsInsightsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  readonly rateLimit = inject(MetaRateLimitService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly route = inject(ActivatedRoute);

  readonly fieldOptions = FIELD_OPTIONS;
  readonly level = signal<InsightLevel>('campaign');
  readonly specificObjectId = signal('');
  readonly datePreset = signal<DatePreset>('last_7d');
  readonly selectedFields = signal<string[]>(FIELD_OPTIONS.map(f => f.id));
  readonly summary = signal<MetaInsightsSummary | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly lastUpdated = signal<Date | null>(null);
  readonly pageApiCallCount = signal(0);

  readonly nameColumnLabel = computed(() => {
    switch (this.level()) {
      case 'adset': return 'Ad Set Name';
      case 'ad': return 'Ad Name';
      case 'account': return 'Period';
      default: return 'Campaign Name';
    }
  });

  readonly tableRows = computed(() => {
    const rows = this.summary()?.rows ?? [];
    const level = this.level();
    return rows.map(row => ({
      name: this.rowLabel(row, level),
      spend: row.spend || '0',
      impressions: row.impressions || '0',
      clicks: row.clicks || '0',
      ctr: row.ctr || '0',
      cpm: row.cpm || '0',
      results: row.results || '0'
    }));
  });

  readonly usagePercent = computed(() =>
    this.rateLimit.bucUsagePercent() || this.rateLimit.adAccountUsagePercent() || this.rateLimit.appUsagePercent()
  );

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    const params = this.route.snapshot.queryParamMap;
    const level = params.get('level');
    const campaignId = params.get('campaignId');
    const adSetId = params.get('adSetId');
    const adId = params.get('adId');

    if (level === 'account' || level === 'campaign' || level === 'adset' || level === 'ad') {
      this.level.set(level);
    }
    if (adId) {
      this.level.set('ad');
      this.specificObjectId.set(adId);
    } else if (adSetId) {
      this.level.set('adset');
      this.specificObjectId.set(adSetId);
    } else if (campaignId) {
      this.level.set('campaign');
      this.specificObjectId.set(campaignId);
    }
  }

  onLevelChange(): void {
    this.specificObjectId.set('');
    this.summary.set(null);
    this.lastUpdated.set(null);
  }

  onDatePresetChange(preset: DatePreset): void {
    this.datePreset.set(preset);
    this.summary.set(null);
    this.lastUpdated.set(null);
  }

  onFieldsChange(fieldId: string, checked: boolean): void {
    this.selectedFields.update(fields => {
      if (checked) return fields.includes(fieldId) ? fields : [...fields, fieldId];
      const next = fields.filter(f => f !== fieldId);
      return next.length ? next : fields;
    });
    this.summary.set(null);
    this.lastUpdated.set(null);
  }

  refreshInsights(): void {
    this.loadInsights();
  }

  exportCsv(): void {
    const rows = this.tableRows();
    if (!rows.length) return;

    const header = [this.nameColumnLabel(), 'Spend', 'Impressions', 'Clicks', 'CTR', 'CPM', 'Results'];
    const lines = [
      header.join(','),
      ...rows.map(r =>
        [r.name, r.spend, r.impressions, r.clicks, r.ctr, r.cpm, r.results]
          .map(v => `"${String(v).replace(/"/g, '""')}"`)
          .join(',')
      )
    ];

    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `meta-insights-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  loadInsights(): void {
    const objectId = this.resolveInsightsObjectId();
    if (!objectId) {
      this.error.set('Select an ad account before loading insights.');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.pageApiCallCount.update(n => n + 1);

    const baseFields = ['campaign_name', 'adset_name', 'ad_name', 'date_start', 'date_stop'];
    const fields = [...new Set([...baseFields, ...this.selectedFields()])].join(',');

    this.api.getInsights(this.processRoute.currentMenuType(), {
      objectId,
      level: this.level(),
      datePreset: this.datePreset(),
      fields
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          this.summary.set(null);
          return;
        }
        this.summary.set(res.data);
        this.lastUpdated.set(new Date());
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load insights from Meta.');
      }
    });
  }

  /** Uses ad account for breakdown levels; specific ID only when deep-linked from another page. */
  private resolveInsightsObjectId(): string {
    const specific = this.specificObjectId().trim();
    if (specific) return specific;

    const account = this.state.selectedAdAccount();
    return account?.id?.trim() ?? '';
  }

  private rowLabel(row: MetaInsightRow, level: InsightLevel): string {
    if (level === 'ad' && row.adName) return row.adName;
    if (level === 'adset' && row.adSetName) return row.adSetName;
    if (level === 'campaign' && row.campaignName) return row.campaignName;
    if (level === 'account' && row.dateStart) {
      return row.dateStop && row.dateStop !== row.dateStart
        ? `${row.dateStart} – ${row.dateStop}`
        : row.dateStart;
    }
    return row.campaignName || row.adSetName || row.adName || '—';
  }
}
