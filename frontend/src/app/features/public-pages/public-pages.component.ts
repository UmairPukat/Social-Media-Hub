import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';
import { ApiResponse, PublicPage } from '../../core/models/api.models';

@Component({
  selector: 'app-public-pages',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule],
  templateUrl: './public-pages.component.html',
  styleUrl: './public-pages.component.scss'
})
export class PublicPagesComponent {
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly processLabel = () =>
    PROCESS_MODULE_LIST.find(m => m.id === this.processRoute.currentMenuType())?.label ?? 'Process';

  readonly query = signal('');
  readonly pages = signal<PublicPage[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly error = signal('');

  search(): void {
    const keyword = this.query().trim();
    if (!keyword) {
      this.error.set('Enter a keyword to search public Pages.');
      this.pages.set([]);
      this.searched.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.searched.set(true);

    this.processApi.searchPublicPages(this.processRoute.currentMenuType(), keyword).subscribe({
      next: (res: ApiResponse<PublicPage[]>) => {
        this.loading.set(false);
        if (!res.success) {
          this.pages.set([]);
          this.error.set(res.message || 'Public Page search failed.');
          return;
        }
        this.pages.set(res.data ?? []);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.pages.set([]);
        this.error.set(err?.error?.message || 'Public Page search failed.');
      }
    });
  }

  onQueryInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
  }
}
