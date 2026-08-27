import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';
import { SocialAccount } from '../../core/models/api.models';

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [DatePipe, MatButtonModule],
  template: `
    <section class="page">
      <h1>Connected Accounts</h1>
      <p>{{ processLabel() }} — SocialAccount + SocialProfiles for this process only.</p>
      <div class="list">
        @for (account of accounts(); track account.id) {
          <article>
            <strong>{{ account.platformName }}</strong>
            <span>{{ account.displayName }}</span>
            <small>{{ account.connectedAt | date: 'medium' }}</small>
            <button mat-stroked-button type="button" (click)="disconnect(account.platformCode)">Disconnect</button>
          </article>
        } @empty {
          <p>No connected accounts yet.</p>
        }
      </div>
    </section>
  `,
  styles: [`
    h1 { font-family: "Space Grotesk", sans-serif; margin: 0 0 6px; }
    p { color:#64748b; }
    .list { display:flex; flex-direction:column; gap:10px; margin-top:16px; }
    article {
      display:flex; gap:12px; align-items:center; flex-wrap:wrap;
      background:#fff; border:1px solid rgba(15,23,42,.06); border-radius:12px; padding:12px 14px;
    }
    small { margin-left:auto; color:#94a3b8; }
  `]
})
export class AccountsComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly processLabel = () =>
    PROCESS_MODULE_LIST.find(m => m.id === this.processRoute.currentMenuType())?.label ?? 'Process';
  readonly accounts = signal<SocialAccount[]>([]);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.processApi.getAccounts(this.processRoute.currentMenuType()).subscribe(res => this.accounts.set(res.data || []));
  }

  disconnect(platformCode: string): void {
    this.processApi.disconnect(this.processRoute.currentMenuType(), platformCode).subscribe(() => this.reload());
  }
}
