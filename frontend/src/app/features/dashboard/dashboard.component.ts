import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/services/api.service';
import { ApiResponse, DashboardSummary } from '../../core/models/api.models';

interface StatCard {
  key: keyof DashboardSummary;
  label: string;
  hint: string;
  icon: string;
  tone: 'blue' | 'green' | 'amber' | 'rose' | 'slate' | 'cyan';
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly error = signal('');

  readonly cards: StatCard[] = [
    { key: 'connectedAccountsCount', label: 'Connected', hint: 'Active social accounts', icon: 'link', tone: 'blue' },
    { key: 'publishedPostsCount', label: 'Published', hint: 'Live posts across channels', icon: 'check_circle', tone: 'green' },
    { key: 'unreadInboxCount', label: 'Unread inbox', hint: 'Comments & messages waiting', icon: 'mark_email_unread', tone: 'amber' },
    { key: 'totalCommentsCount', label: 'Comments', hint: 'Tracked engagement threads', icon: 'mode_comment', tone: 'cyan' },
    { key: 'totalMessagesCount', label: 'Messages', hint: 'Direct conversations', icon: 'forum', tone: 'slate' },
    { key: 'failedPostsCount', label: 'Failed posts', hint: 'Needs attention', icon: 'error_outline', tone: 'rose' }
  ];

  readonly health = computed(() => {
    const s = this.summary();
    if (!s) return null;
    const total = s.publishedPostsCount + s.failedPostsCount + s.scheduledPostsCount;
    const okRate = total === 0 ? 100 : Math.round((s.publishedPostsCount / total) * 100);
    return {
      okRate,
      scheduled: s.scheduledPostsCount,
      totalPosts: s.totalPostsCount
    };
  });

  ngOnInit(): void {
    this.api.getDashboard().subscribe({
      next: (res: ApiResponse<DashboardSummary>) => this.summary.set(res.data),
      error: () => this.error.set('Unable to load dashboard')
    });
  }

  value(key: keyof DashboardSummary): number {
    return Number(this.summary()?.[key] ?? 0);
  }
}
