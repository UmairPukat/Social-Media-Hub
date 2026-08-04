import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import { DashboardSummary } from '../../core/models/api.models';

@Component({
  selector: 'app-analytics',
  standalone: true,
  template: `
    <section>
      <h1>Analytics</h1>
      <p>High-level engagement snapshot based on connected activity.</p>
      @if (summary(); as s) {
        <div class="bars">
          <div class="bar"><label>Published posts</label><div class="track"><i [style.width.%]="pct(s.publishedPostsCount)"></i></div><span>{{ s.publishedPostsCount }}</span></div>
          <div class="bar"><label>Comments</label><div class="track"><i [style.width.%]="pct(s.totalCommentsCount)"></i></div><span>{{ s.totalCommentsCount }}</span></div>
          <div class="bar"><label>Messages</label><div class="track"><i [style.width.%]="pct(s.totalMessagesCount)"></i></div><span>{{ s.totalMessagesCount }}</span></div>
          <div class="bar"><label>Unread inbox</label><div class="track"><i [style.width.%]="pct(s.unreadInboxCount)"></i></div><span>{{ s.unreadInboxCount }}</span></div>
        </div>
      }
    </section>
  `,
  styles: [`
    h1 { font-family: "Space Grotesk", sans-serif; margin: 0 0 6px; }
    p { color:#64748b; }
    .bars { display:flex; flex-direction:column; gap:14px; max-width:640px; margin-top:18px; }
    .bar { display:grid; grid-template-columns:140px 1fr 40px; gap:10px; align-items:center; }
    .track { height:10px; background:#e2e8f0; border-radius:999px; overflow:hidden; }
    i { display:block; height:100%; background:linear-gradient(90deg,#1877f2,#25d366); }
  `]
})
export class AnalyticsComponent implements OnInit {
  readonly summary = signal<DashboardSummary | null>(null);

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getDashboard().subscribe(res => this.summary.set(res.data));
  }

  pct(value: number): number {
    return Math.min(100, value * 8);
  }
}
