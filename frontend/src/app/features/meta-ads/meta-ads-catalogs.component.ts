import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaCatalog } from '../../core/models/meta-ads.models';
import { metaCommerceCatalogUrl, metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-catalogs',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './meta-ads-catalogs.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsCatalogsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly catalogs = signal<MetaCatalog[]>([]);
  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly selectedId = signal<string | null>(null);

  ngOnInit(): void {
    this.state.syncForCurrentProcess();
    this.selectedId.set(this.state.selectedCatalog()?.id ?? null);
    this.load();
  }

  load(after?: string): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getCatalogs(this.processRoute.currentMenuType(), after).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.catalogs.set(res.data?.items ?? []);
        this.nextCursor.set(res.data?.nextCursor ?? null);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load catalogs.');
      }
    });
  }

  createDemoCatalog(): void {
    this.creating.set(true);
    this.error.set('');
    this.api
      .createCatalog(this.processRoute.currentMenuType(), {
        name: 'App Review Demo Catalog',
        vertical: 'commerce'
      })
      .subscribe({
        next: (res) => {
          this.creating.set(false);
          if (!res.success || !res.data) {
            this.error.set(metaErrorMessage(res));
            return;
          }
          this.banner.set(`Catalog created: ${res.data.name} (${res.data.id})`);
          this.select(res.data);
          this.load();
        },
        error: () => {
          this.creating.set(false);
          this.error.set('Unable to create catalog.');
        }
      });
  }

  select(catalog: MetaCatalog): void {
    this.selectedId.set(catalog.id);
    this.state.selectCatalog({
      id: catalog.id,
      name: catalog.name,
      businessId: catalog.businessId
    });
    this.banner.set(`Selected catalog: ${catalog.name} (${catalog.id})`);
  }

  isSelected(catalog: MetaCatalog): boolean {
    return this.selectedId() === catalog.id;
  }

  commerceUrl(catalog: MetaCatalog): string {
    return metaCommerceCatalogUrl(catalog.id);
  }
}
