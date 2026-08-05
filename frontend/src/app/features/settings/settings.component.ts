import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ThemeService } from '../../core/services/theme.service';
import { ChromeThemeId } from '../../core/theme/chrome-themes';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <section class="page settings">
      <header class="page-header">
        <div>
          <h1>Settings</h1>
          <p>Manage workspace preferences, including navbar and sidebar chrome themes.</p>
        </div>
      </header>

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
export class SettingsComponent {
  readonly theme = inject(ThemeService);

  select(id: ChromeThemeId): void {
    this.theme.setTheme(id);
  }
}
