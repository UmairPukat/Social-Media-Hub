import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/services/api.service';
import { DashboardSummary } from '../../core/models/api.models';

type AnalyticsRange = 7 | 30 | 90;

interface ChartPoint {
  x: number;
  y: number;
  value: number;
  label: string;
}

interface PlatformPerformance {
  code: string;
  name: string;
  icon: string;
  color: string;
  reach: number;
  engagement: number;
  growth: number;
  share: number;
}

const DEFAULT_SUMMARY: DashboardSummary = {
  connectedAccountsCount: 7,
  totalPostsCount: 86,
  publishedPostsCount: 72,
  failedPostsCount: 3,
  scheduledPostsCount: 11,
  unreadInboxCount: 24,
  totalCommentsCount: 1842,
  totalMessagesCount: 963
};

const SERIES: Record<AnalyticsRange, number[]> = {
  7: [41800, 46300, 43900, 52100, 58700, 55200, 68400],
  30: [24, 29, 27, 34, 31, 38, 42, 39, 48, 44, 51, 56, 52, 61, 58, 66, 63, 71, 68, 76, 73, 81, 78, 88, 84, 91, 96, 93, 101, 108].map((v) => v * 1000),
  90: [182, 194, 188, 207, 216, 228, 221, 243, 251, 264, 258, 279, 291, 286, 307, 318, 329, 324].map((v) => v * 1000)
};

const PLATFORM_PERFORMANCE: PlatformPerformance[] = [
  { code: 'instagram', name: 'Instagram', icon: 'photo_camera', color: '#e4405f', reach: 186400, engagement: 18420, growth: 14.8, share: 38 },
  { code: 'facebook', name: 'Facebook', icon: 'public', color: '#1877f2', reach: 142800, engagement: 10360, growth: 8.4, share: 29 },
  { code: 'tiktok', name: 'TikTok', icon: 'music_note', color: '#111111', reach: 98200, engagement: 7240, growth: 21.6, share: 20 },
  { code: 'linkedin', name: 'LinkedIn', icon: 'work', color: '#0a66c2', reach: 41800, engagement: 1980, growth: 6.2, share: 9 },
  { code: 'twitter', name: 'X', icon: 'alternate_email', color: '#0f1419', reach: 17000, engagement: 840, growth: 3.1, share: 4 }
];

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [DecimalPipe, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss'
})
export class AnalyticsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly summary = signal<DashboardSummary>(DEFAULT_SUMMARY);
  readonly range = signal<AnalyticsRange>(30);
  readonly loading = signal(true);
  readonly previewMode = signal(false);
  readonly notice = signal('');
  readonly platforms = PLATFORM_PERFORMANCE;
  readonly ranges: { value: AnalyticsRange; label: string }[] = [
    { value: 7, label: '7 days' },
    { value: 30, label: '30 days' },
    { value: 90, label: '90 days' }
  ];

  readonly series = computed(() => SERIES[this.range()]);
  readonly chartPoints = computed<ChartPoint[]>(() => {
    const values = this.series();
    const max = Math.max(...values) * 1.12;
    const width = 800;
    const height = 220;
    return values.map((value, index) => ({
      x: values.length === 1 ? 0 : (index / (values.length - 1)) * width,
      y: height - (value / max) * height,
      value,
      label: this.pointLabel(index, values.length)
    }));
  });

  readonly linePath = computed(() =>
    this.chartPoints().map((point, index) => `${index ? 'L' : 'M'} ${point.x} ${point.y}`).join(' ')
  );
  readonly areaPath = computed(() => `${this.linePath()} L 800 240 L 0 240 Z`);
  readonly reach = computed(() => this.series().reduce((sum, value) => sum + value, 0));
  readonly engagement = computed(() => Math.round(this.reach() * 0.079));
  readonly axisLabels = computed(() =>
    this.range() === 7
      ? ['Mon', 'Wed', 'Fri', 'Sun']
      : this.range() === 30
        ? ['Jul 8', 'Jul 15', 'Jul 22', 'Aug 6']
        : ['May', 'Jun', 'Jul', 'Aug']
  );
  readonly publishingRate = computed(() => {
    const summary = this.summary();
    const resolved = summary.publishedPostsCount + summary.failedPostsCount;
    return resolved ? Math.round((summary.publishedPostsCount / resolved) * 100) : 100;
  });

  readonly topContent = [
    { platform: 'Instagram', color: '#e4405f', icon: 'photo_camera', title: 'A simpler way to run your social workflow ✨', type: 'Reel', reach: 68400, engagement: 9210, rate: 13.5 },
    { platform: 'TikTok', color: '#111111', icon: 'music_note', title: '3 reporting shortcuts every social manager needs', type: 'Video', reach: 52100, engagement: 6840, rate: 13.1 },
    { platform: 'Facebook', color: '#1877f2', icon: 'public', title: 'One workspace. Every conversation. Every channel.', type: 'Post', reach: 38600, engagement: 3210, rate: 8.3 },
    { platform: 'LinkedIn', color: '#0a66c2', icon: 'work', title: 'Building a connected social operation', type: 'Document', reach: 18400, engagement: 1260, rate: 6.8 }
  ];

  ngOnInit(): void {
    this.api.getDashboard().subscribe({
      next: (res) => {
        this.summary.set(res.data || DEFAULT_SUMMARY);
        this.loading.set(false);
      },
      error: () => {
        this.previewMode.set(true);
        this.loading.set(false);
      }
    });
  }

  setRange(range: AnalyticsRange): void {
    this.range.set(range);
  }

  compact(value: number): string {
    return Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
  }

  exportReport(): void {
    this.notice.set('Analytics report prepared for export. File download can be connected to the reporting API.');
  }

  private pointLabel(index: number, count: number): string {
    if (this.range() === 7) return ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][index];
    if (this.range() === 30) return `Day ${index + 1}`;
    return `Week ${index + 1}`;
  }
}
