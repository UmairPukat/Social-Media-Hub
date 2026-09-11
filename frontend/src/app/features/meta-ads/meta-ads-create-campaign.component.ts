import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-create-campaign',
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  templateUrl: './meta-ads-create-campaign.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsCreateCampaignComponent {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly name = signal('App Review Demo Campaign');
  readonly objective = signal('OUTCOME_AWARENESS');
  readonly status = signal('PAUSED');
  readonly specialCategory = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly createdId = signal('');

  readonly objectives = [
    'OUTCOME_AWARENESS',
    'OUTCOME_ENGAGEMENT',
    'OUTCOME_LEADS',
    'OUTCOME_SALES',
    'OUTCOME_TRAFFIC',
    'OUTCOME_APP_PROMOTION'
  ];

  create(): void {
    const adAccount = this.state.selectedAdAccount();
    if (!adAccount) return;

    this.loading.set(true);
    this.error.set('');
    this.createdId.set('');

    const special = this.specialCategory().trim();
    this.api
      .createCampaign(this.processRoute.currentMenuType(), {
        adAccountId: adAccount.id,
        name: this.name().trim(),
        objective: this.objective(),
        status: this.status(),
        specialAdCategories: special ? [special] : undefined
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (!res.success) {
            this.error.set(metaErrorMessage(res));
            return;
          }

          const campaignId = res.data?.id ?? '';
          this.createdId.set(campaignId);
          void this.router.navigate(['../campaigns'], {
            relativeTo: this.route,
            queryParams: { created: campaignId }
          });
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Unable to create campaign.');
        }
      });
  }
}
