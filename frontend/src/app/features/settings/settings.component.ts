import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaRateLimitService } from '../../core/services/meta-rate-limit.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { ThemeService } from '../../core/services/theme.service';
import { MetaApiHealth } from '../../core/models/meta-ads.models';
import { ChromeThemeId } from '../../core/theme/chrome-themes';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <section class="page settings">
      <header class="page-header">
        <div>
          <h1>Settings</h1>
          <p>Manage workspace preferences, API health, and chrome themes.</p>
        </div>
      </header>

      <section class="panel theme-panel">
        <div class="panel-head">
          <div>
            <h2>API Health</h2>
            <p>Marketing API usage metrics for Meta Advanced Access review.</p>
          </div>
          <button mat-stroked-button type="button" (click)="loadHealth()" [disabled]="healthLoading()">
            <mat-icon>refresh</mat-icon> Refresh
          </button>
        </div>

        @if (healthLoading()) {
          <p class="muted">Loading API health…</p>
        } @else if (health()) {
          <div class="health-grid">
            <div class="health-stat">
              <small>15-day API calls</small>
              <strong>{{ health()!.totalCalls15Days }}</strong>
            </div>
            <div class="health-stat">
              <small>Error rate</small>
              <strong>{{ health()!.errorRatePercent }}%</strong>
            </div>
            <div class="health-stat wide">
              <small>Current tier</small>
              <strong>{{ health()!.tierLabel }} — {{ health()!.tierStatus }}</strong>
            </div>
            <div class="health-stat">
              <small>Current BUC usage</small>
              <strong>{{ currentUsage() }}%</strong>
            </div>
          </div>

          <div class="health-chart" aria-label="API calls per day chart">
            @for (point of chartBars(); track point.date) {
              <div class="bar-col" [title]="point.date + ': ' + point.calls + ' calls'">
                <div class="bar" [style.height.%]="point.height"></div>
                <small>{{ point.label }}</small>
              </div>
            }
          </div>

          <p class="muted log-note">
            {{ localLogCount() }} Graph API calls logged locally for debugging.
            <button mat-button type="button" (click)="clearLocalLog()">Clear log</button>
          </p>
        }
      </section>

      <section class="panel theme-panel">
        <div class="panel-head">
          <div>
            <h2>Chrome theme</h2>
            <p>Choose a professional look for the top navbar and sidebar. Your choice is saved on this device.</p>
          </div>
          <span class="current">Active: {{ theme.activeTheme().name }}</span>
        </div>

        <div class="theme-grid">
          @for (item of theme.themes; track item.id) {
            <button
              type="button"
              class="theme-card"
              [class.active]="theme.themeId() === item.id"
              (click)="select(item.id)">
              <div class="swatches" aria-hidden="true">
                <span class="swatch" [style.background]="item.preview.navbar"></span>
                <span class="swatch" [style.background]="item.preview.sidebar"></span>
                <span class="swatch" [style.background]="item.preview.accent"></span>
              </div>
              <div class="mini-chrome" aria-hidden="true">
                <div class="mini-nav" [style.background]="item.preview.navbar" [style.border-color]="item.preview.accent"></div>
                <div class="mini-body">
                  <div class="mini-side" [style.background]="item.preview.sidebar"></div>
                  <div class="mini-main" [style.background]="item.preview.main">
                    <span [style.border-color]="item.preview.accent"></span>
                    <span [style.border-color]="item.preview.accent"></span>
                  </div>
                </div>
              </div>
              <div class="theme-copy">
                <strong>{{ item.name }}</strong>
                <small>{{ item.description }}</small>
              </div>
              @if (theme.themeId() === item.id) {
                <span class="check"><mat-icon>check_circle</mat-icon> Selected</span>
              }
            </button>
          }
        </div>
      </section>

      <section class="panel">
        <h2>Environment</h2>
        <p>API base URL comes from <code>environment.ts</code> (local) or <code>environment.prod.ts</code> / Railway <code>API_URL</code> (production).</p>
        <h2>Meta apps</h2>
        <p>Facebook, Instagram, and WhatsApp credentials live in backend <code>appsettings.json</code> under <code>MetaSettings</code>.</p>
        <h2>Default admin</h2>
        <p>Email: <code>Admin&#64;gmail.com</code> · Password: <code>Admin&#64;321</code></p>
        <h2>Invite token</h2>
        <p><code>INVITE-SOCIALHUB-2026</code></p>
      </section>
    </section>
  `,
  styles: [`
    .settings {
      max-width: 1100px;
    }

    .page-header h1 {
      margin: 0 0 6px;
      font-family: "Sora", "Space Grotesk", sans-serif;
      letter-spacing: -0.03em;
    }

    .page-header p {
      margin: 0;
      color: #64748b;
    }

    .panel {
      margin-top: 18px;
      background: #fff;
      border: 1px solid rgba(15, 23, 42, 0.06);
      border-radius: 18px;
      padding: 20px;
      box-shadow: 0 10px 28px rgba(15, 23, 42, 0.04);
    }

    .panel h2 {
      margin: 18px 0 6px;
      font-family: "Sora", "Space Grotesk", sans-serif;
      font-size: 1.05rem;
    }

    .panel h2:first-child { margin-top: 0; }

    .panel p {
      margin: 0 0 4px;
      color: #64748b;
      line-height: 1.5;
    }

    .muted {
      color: #64748b;
    }

    code {
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 6px;
      font-size: 0.88em;
    }

    .panel-head {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      align-items: flex-start;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }

    .panel-head h2 {
      margin: 0 0 6px;
    }

    .current {
      font-size: 0.82rem;
      font-weight: 700;
      color: #1d4ed8;
      background: #eff6ff;
      border: 1px solid rgba(37, 99, 235, 0.16);
      border-radius: 999px;
      padding: 6px 12px;
    }

    .health-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 18px;
    }

    .health-stat {
      padding: 14px;
      border-radius: 14px;
      border: 1px solid rgba(15, 23, 42, 0.08);
      background: #f8fafc;
    }

    .health-stat.wide {
      grid-column: span 2;
    }

    .health-stat small {
      display: block;
      color: #64748b;
      margin-bottom: 4px;
      font-size: 0.78rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-weight: 700;
    }

    .health-stat strong {
      color: #0f172a;
      font-family: "Sora", sans-serif;
      font-size: 1.2rem;
    }

    .health-chart {
      display: grid;
      grid-template-columns: repeat(15, minmax(0, 1fr));
      gap: 6px;
      align-items: end;
      min-height: 160px;
      padding: 12px;
      border-radius: 14px;
      border: 1px solid rgba(15, 23, 42, 0.08);
      background: #fff;
      overflow-x: auto;
    }

    .bar-col {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      min-width: 28px;
    }

    .bar {
      width: 100%;
      max-width: 28px;
      border-radius: 6px 6px 2px 2px;
      background: linear-gradient(180deg, #2563eb, #93c5fd);
      min-height: 4px;
    }

    .bar-col small {
      font-size: 0.62rem;
      color: #64748b;
      writing-mode: vertical-rl;
      transform: rotate(180deg);
    }

    .log-note {
      margin-top: 12px;
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }

    .theme-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 14px;
    }

    .theme-card {
      text-align: left;
      border: 1px solid rgba(15, 23, 42, 0.08);
      background: #f8fafc;
      border-radius: 16px;
      padding: 14px;
      cursor: pointer;
      font: inherit;
      color: inherit;
      display: flex;
      flex-direction: column;
      gap: 10px;
      transition: transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease;
    }

    .theme-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 12px 28px rgba(15, 23, 42, 0.08);
    }

    .theme-card.active {
      border-color: rgba(37, 99, 235, 0.45);
      background: #fff;
      box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
    }

    .swatches {
      display: flex;
      gap: 6px;
    }

    .swatch {
      width: 18px;
      height: 18px;
      border-radius: 6px;
      border: 1px solid rgba(15, 23, 42, 0.12);
    }

    .mini-chrome {
      border-radius: 10px;
      overflow: hidden;
      border: 1px solid rgba(15, 23, 42, 0.08);
      background: #e2e8f0;
    }

    .mini-nav {
      height: 14px;
      border-bottom: 2px solid;
    }

    .mini-body {
      display: grid;
      grid-template-columns: 34px 1fr;
      height: 54px;
    }

    .mini-side { border-right: 1px solid rgba(15, 23, 42, 0.06); }
    .mini-main {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 5px;
      padding: 8px;
    }

    .mini-main span {
      display: block;
      border: 1px solid;
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.55);
    }

    .theme-copy strong {
      display: block;
      font-size: 0.95rem;
      margin-bottom: 4px;
    }

    .theme-copy small {
      color: #64748b;
      font-size: 0.8rem;
      line-height: 1.4;
    }

    .check {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: #1d4ed8;
      font-size: 0.8rem;
      font-weight: 700;
    }

    .check mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
  `]
})
export class SettingsComponent implements OnInit {
  readonly theme = inject(ThemeService);
  private readonly api = inject(MetaAdsApiService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly rateLimit = inject(MetaRateLimitService);

  readonly health = signal<MetaApiHealth | null>(null);
  readonly healthLoading = signal(false);

  readonly currentUsage = computed(() =>
    Math.max(
      this.rateLimit.bucUsagePercent(),
      this.health()?.currentUsage?.overallPercent ?? 0
    )
  );

  readonly localLogCount = computed(() => this.rateLimit.readLog().length);

  readonly chartBars = computed(() => {
    const points = this.health()?.dailyCalls ?? [];
    const max = Math.max(...points.map(p => p.calls), 1);
    return points.map(p => ({
      date: p.date,
      calls: p.calls,
      label: p.date.slice(5),
      height: Math.max(8, (p.calls / max) * 100)
    }));
  });

  ngOnInit(): void {
    this.loadHealth();
  }

  loadHealth(): void {
    this.healthLoading.set(true);
    this.api.getApiHealth(this.processRoute.currentMenuType()).subscribe({
      next: (res) => {
        this.healthLoading.set(false);
        if (res.success) this.health.set(res.data);
      },
      error: () => this.healthLoading.set(false)
    });
  }

  clearLocalLog(): void {
    this.rateLimit.clearLog();
  }

  select(id: ChromeThemeId): void {
    this.theme.setTheme(id);
  }
}
