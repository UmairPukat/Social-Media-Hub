import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaCatalog } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

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
  readonly error = signal('');
  readonly banner = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly selectedId = signal<string | null>(this.state.selectedCatalog()?.id ?? null);

  ngOnInit(): void {
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

  select(catalog: MetaCatalog): void {
    this.selectedId.set(catalog.id);
    this.state.selectCatalog({ id: catalog.id, name: catalog.name });
    this.banner.set(`Selected catalog: ${catalog.name} (${catalog.id})`);
  }

  isSelected(catalog: MetaCatalog): boolean {
    return this.selectedId() === catalog.id;
  }
}
