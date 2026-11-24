import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';
import { ReadInsightPoint, ReadInsightsReport } from '../../core/models/api.models';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';

type InsightDays = 7 | 28 | 90;
type SeriesKey = 'pageViews' | 'reach' | 'impressions' | 'engagement' | 'followers';

interface SeriesDef {
  key: SeriesKey;
  label: string;
  color: string;
}

interface ChartPoint {
  x: number;
  y: number;
  value: number;
  label: string;
}

const SERIES: SeriesDef[] = [
  { key: 'reach', label: 'Reach', color: '#1877f2' },
  { key: 'impressions', label: 'Impressions', color: '#0f766e' },
  { key: 'engagement', label: 'Engagement', color: '#d97706' },
  { key: 'pageViews', label: 'Page views', color: '#7c3aed' },
  { key: 'followers', label: 'Followers', color: '#be123c' }
];

@Component({
  selector: 'app-read-insights',
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule],
  templateUrl: './read-insights.component.html',
  styleUrl: './read-insights.component.scss'
})
export class ReadInsightsComponent implements OnInit {
  private readonly api = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly days = signal<InsightDays>(28);
  readonly platform = signal<string>('');
  readonly report = signal<ReadInsightsReport | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly hidden = signal<SeriesKey[]>(['followers']);
  readonly ranges: { value: InsightDays; label: string }[] = [
    { value: 7, label: '7 days' },
    { value: 28, label: '28 days' },
    { value: 90, label: '90 days' }
  ];

  readonly processLabel = () =>
    PROCESS_MODULE_LIST.find(module => module.id === this.processRoute.currentMenuType())?.label ?? 'Module';

  readonly activeSeries = computed(() => {
    const report = this.report();
    if (!report) return [] as SeriesDef[];
    const hidden = new Set(this.hidden());
    return SERIES.filter(series => this.seriesSupported(report, series.key) && !hidden.has(series.key));
  });

  readonly chart = computed(() => this.buildChart(this.report()?.trend ?? [], this.activeSeries(), false));
  readonly followerChart = computed(() => {
    const report = this.report();
    if (!report?.followersSupported) return null;
    return this.buildChart(report.trend, [SERIES[4]], true);
  });

  ngOnInit(): void {
    this.load();
  }

  setDays(days: InsightDays): void {
    if (this.days() === days) return;
    this.days.set(days);
    this.load();
  }

  setPlatform(platform: string): void {
    if (this.platform() === platform) return;
    this.platform.set(platform);
    this.load();
  }

  toggleSeries(key: SeriesKey): void {
    this.hidden.update(list => list.includes(key) ? list.filter(item => item !== key) : [...list, key]);
  }

  seriesHidden(key: SeriesKey): boolean {
    return this.hidden().includes(key);
  }

  platformLabel(code: string): string {
    return code === 'instagram' ? 'Instagram' : 'Facebook';
  }

  metricValue(value: number | null | undefined, supported: boolean): string {
    if (!supported || value === null || value === undefined) return 'Not supported';
    return new Intl.NumberFormat().format(value);
  }

  growthLabel(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    const formatted = new Intl.NumberFormat().format(Math.abs(value));
    if (value > 0) return `+${formatted} in this period`;
    if (value < 0) return `-${formatted} in this period`;
    return 'No change in this period';
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getReadInsights(this.processRoute.currentMenuType(), this.days(), this.platform() || undefined).subscribe({
      next: res => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.report.set(null);
          this.error.set(res.message || 'Insights could not be loaded.');
          return;
        }
        this.report.set(res.data);
        if (!this.platform()) this.platform.set(res.data.platform);
      },
      error: () => {
        this.loading.set(false);
        this.report.set(null);
        this.error.set('Insights could not be loaded.');
      }
    });
  }

  private seriesSupported(report: ReadInsightsReport, key: SeriesKey): boolean {
    switch (key) {
      case 'pageViews': return report.pageViewsSupported;
      case 'reach': return report.reachSupported;
      case 'impressions': return report.impressionsSupported;
      case 'engagement': return report.engagementSupported;
      case 'followers': return report.followersSupported;
    }
  }

  private buildChart(trend: ReadInsightPoint[], series: SeriesDef[], includeFollowers: boolean) {
    const usable = series.filter(item => includeFollowers || item.key !== 'followers');
    const points = trend.filter(point => point.date && point.date !== 'total');
    if (!usable.length) return null;

    const width = 760;
    const height = 240;
    if (!points.length) return null;
    const values = points.flatMap(point => usable.map(item => Number(point[item.key] ?? 0)));
    const peak = Math.max(1, ...values);
    const scale = peak * 1.08;
    const lines = usable.map(item => {
      const plotted: ChartPoint[] = points.map((point, index) => {
        const value = Number(point[item.key] ?? 0);
        return {
          x: points.length === 1 ? width / 2 : (index / (points.length - 1)) * width,
          y: height - (value / scale) * height,
          value,
          label: point.date
        };
      });
      return {
        ...item,
        points: plotted,
        path: plotted.map((point, index) => `${index ? 'L' : 'M'} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`).join(' ')
      };
    });

    const ticks = [0, Math.round(peak / 2), peak];
    return { width, height, scale, lines, ticks, labels: this.axisLabels(points) };
  }

  private axisLabels(points: ReadInsightPoint[]): string[] {
    if (points.length <= 4) return points.map(point => this.shortDate(point.date));
    const indexes = [0, Math.floor(points.length / 3), Math.floor((points.length * 2) / 3), points.length - 1];
    return [...new Set(indexes)].map(index => this.shortDate(points[index].date));
  }

  private shortDate(value: string): string {
    const date = new Date(`${value}T00:00:00`);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
}
