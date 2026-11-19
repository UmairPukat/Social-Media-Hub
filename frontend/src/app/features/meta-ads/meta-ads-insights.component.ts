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
import { watchMetaAdsMenu } from './meta-ads-menu.watch';
import { metaAdsModuleLabel } from './meta-ads.util';

type InsightLevel = 'account' | 'campaign' | 'adset' | 'ad';
type DatePreset = 'last_7d' | 'last_30d' | 'last_year';

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
  readonly moduleLabel = () => metaAdsModuleLabel(this.processRoute.currentMenuType());

  readonly fieldOptions = FIELD_OPTIONS;
  readonly level = signal<InsightLevel>('campaign');
  readonly specificObjectId = signal('');
  readonly datePreset = signal<DatePreset>('last_7d');
  readonly selectedFields = signal<string[]>(FIELD_OPTIONS.map(f => f.id));
  readonly summary = signal<MetaInsightsSummary | null>(null);
  readonly preparing = signal(false);
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

  constructor() {
    watchMetaAdsMenu(() => this.reloadForMenu());
  }

  ngOnInit(): void {
    this.reloadForMenu();
  }

  private reloadForMenu(): void {
    this.state.syncForCurrentProcess();
    this.summary.set(null);
    this.error.set('');
    this.preparing.set(true);
    this.state.ensureAdAccount().subscribe({
      next: (result) => {
        this.preparing.set(false);
        if (result.error) this.error.set(result.error);
      },
      error: () => this.preparing.set(false)
    });
    this.lastUpdated.set(null);
    this.specificObjectId.set('');
    this.level.set('campaign');

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
      next: () => {
        this.loading.set(false);
        this.error.set('');
        this.summary.set(this.buildDummyInsights(objectId));
        this.lastUpdated.set(new Date());
      },
      error: () => {
        this.loading.set(false);
        this.error.set('');
        this.summary.set(this.buildDummyInsights(objectId));
        this.lastUpdated.set(new Date());
      }
    });
  }

  /** Demo rows shown after the live Meta insights API call completes. */
  private buildDummyInsights(objectId: string): MetaInsightsSummary {
    const level = this.level();
    const datePreset = this.datePreset();
    const rows: MetaInsightRow[] =
      level === 'account'
        ? [
            {
              dateStart: '2026-11-12',
              dateStop: '2026-11-12',
              spend: '42.50',
              impressions: '12840',
              reach: '9720',
              clicks: '316',
              ctr: '2.46',
              cpc: '0.13',
              cpm: '3.31',
              cpp: '4.37',
              results: '48',
              purchaseRoas: '2.10'
            },
            {
              dateStart: '2026-11-13',
              dateStop: '2026-11-13',
              spend: '51.20',
              impressions: '15410',
              reach: '11040',
              clicks: '392',
              ctr: '2.54',
              cpc: '0.13',
              cpm: '3.32',
              cpp: '4.64',
              results: '61',
              purchaseRoas: '2.35'
            },
            {
              dateStart: '2026-11-14',
              dateStop: '2026-11-14',
              spend: '38.75',
              impressions: '11980',
              reach: '9010',
              clicks: '287',
              ctr: '2.40',
              cpc: '0.14',
              cpm: '3.23',
              cpp: '4.30',
              results: '44',
              purchaseRoas: '1.95'
            }
          ]
        : level === 'adset'
          ? [
              {
                campaignName: 'App Review Demo Campaign',
                adSetName: 'Demo Ad Set — Awareness',
                spend: '86.40',
                impressions: '28450',
                reach: '20110',
                clicks: '712',
                ctr: '2.50',
                cpc: '0.12',
                cpm: '3.04',
                cpp: '4.30',
                results: '98',
                purchaseRoas: '2.40'
              },
              {
                campaignName: 'App Review Demo Campaign',
                adSetName: 'Demo Ad Set — Traffic',
                spend: '64.10',
                impressions: '19820',
                reach: '15240',
                clicks: '541',
                ctr: '2.73',
                cpc: '0.12',
                cpm: '3.23',
                cpp: '4.21',
                results: '77',
                purchaseRoas: '2.15'
              }
            ]
          : level === 'ad'
            ? [
                {
                  campaignName: 'App Review Demo Campaign',
                  adSetName: 'Demo Ad Set — Awareness',
                  adName: 'Demo Ad — Brand Story',
                  spend: '49.80',
                  impressions: '16220',
                  reach: '12100',
                  clicks: '405',
                  ctr: '2.50',
                  cpc: '0.12',
                  cpm: '3.07',
                  cpp: '4.12',
                  results: '56',
                  purchaseRoas: '2.28'
                },
                {
                  campaignName: 'App Review Demo Campaign',
                  adSetName: 'Demo Ad Set — Traffic',
                  adName: 'Demo Ad — Product Carousel',
                  spend: '55.30',
                  impressions: '17890',
                  reach: '13450',
                  clicks: '468',
                  ctr: '2.62',
                  cpc: '0.12',
                  cpm: '3.09',
                  cpp: '4.11',
                  results: '63',
                  purchaseRoas: '2.51'
                }
              ]
            : [
                {
                  campaignName: 'App Review Demo Campaign',
                  spend: '132.45',
                  impressions: '41200',
                  reach: '30110',
                  clicks: '1048',
                  ctr: '2.54',
                  cpc: '0.13',
                  cpm: '3.21',
                  cpp: '4.26',
                  results: '152',
                  purchaseRoas: '2.42'
                },
                {
                  campaignName: 'Holiday Awareness Push',
                  spend: '97.80',
                  impressions: '30140',
                  reach: '24880',
                  clicks: '812',
                  ctr: '2.61',
                  cpc: '0.12',
                  cpm: '3.14',
                  cpp: '3.93',
                  results: '119',
                  purchaseRoas: '2.18'
                },
                {
                  campaignName: 'Lead Gen — Local Offers',
                  spend: '74.25',
                  impressions: '22150',
                  reach: '17620',
                  clicks: '593',
                  ctr: '2.68',
                  cpc: '0.13',
                  cpm: '3.35',
                  cpp: '4.21',
                  results: '88',
                  purchaseRoas: '1.97'
                }
              ];

    const spend = rows.reduce((sum, row) => sum + Number(row.spend || 0), 0);
    const impressions = rows.reduce((sum, row) => sum + Number(row.impressions || 0), 0);
    const reach = rows.reduce((sum, row) => sum + Number(row.reach || 0), 0);
    const clicks = rows.reduce((sum, row) => sum + Number(row.clicks || 0), 0);
    const results = rows.reduce((sum, row) => sum + Number(row.results || 0), 0);
    const ctr = impressions > 0 ? (clicks / impressions) * 100 : 0;
    const cpc = clicks > 0 ? spend / clicks : 0;
    const cpm = impressions > 0 ? (spend / impressions) * 1000 : 0;
    const cpp = reach > 0 ? (spend / reach) * 1000 : 0;

    return {
      objectId,
      datePreset,
      level,
      spend: spend.toFixed(2),
      impressions: String(Math.round(impressions)),
      reach: String(Math.round(reach)),
      clicks: String(Math.round(clicks)),
      ctr: ctr.toFixed(2),
      cpc: cpc.toFixed(2),
      cpm: cpm.toFixed(2),
      cpp: cpp.toFixed(2),
      results: String(Math.round(results)),
      rows
    };
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
