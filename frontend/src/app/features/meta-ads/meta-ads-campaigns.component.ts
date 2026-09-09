import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaCampaign } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-campaigns',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './meta-ads-campaigns.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsCampaignsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly campaigns = signal<MetaCampaign[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');
  readonly status = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly cursorStack = signal<string[]>([]);

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.campaigns().filter(c => {
      if (this.status() && (c.status || '').toUpperCase() !== this.status()) return false;
      if (!term) return true;
      return c.name.toLowerCase().includes(term) || c.id.includes(term);
    });
  });

  ngOnInit(): void {
    this.load();
  }

  load(after?: string): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) {
      this.campaigns.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api
      .getCampaigns(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        after,
        limit: 25
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.campaigns.set(res.data?.items ?? []);
          this.nextCursor.set(res.data?.nextCursor ?? null);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Unable to load campaigns.');
        }
      });
  }

  refresh(): void {
    this.cursorStack.set([]);
    this.load();
  }

  nextPage(): void {
    const cursor = this.nextCursor();
    if (!cursor) return;
    this.cursorStack.update(stack => [...stack, cursor]);
    this.load(cursor);
  }

  prevPage(): void {
    const stack = [...this.cursorStack()];
    stack.pop();
    const prev = stack.at(-1);
    this.cursorStack.set(stack);
    this.load(prev);
  }

  pause(campaign: MetaCampaign): void {
    this.updateStatus(campaign, 'PAUSED');
  }

  resume(campaign: MetaCampaign): void {
    this.updateStatus(campaign, 'ACTIVE');
  }

  private updateStatus(campaign: MetaCampaign, status: string): void {
    this.api.updateCampaign(this.processRoute.currentMenuType(), campaign.id, { status }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set(`Campaign ${campaign.name} is now ${status}.`);
        this.refresh();
      },
      error: () => this.error.set('Unable to update campaign.')
    });
  }
}
