import { Component, computed, inject, signal } from '@angular/core';
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
  readonly selectedId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly error = signal('');

  readonly selected = computed(() => {
    const pages = this.pages();
    if (pages.length === 0) return null;
    const id = this.selectedId();
    return pages.find(page => this.pageKey(page) === id) ?? pages[0];
  });

  search(): void {
    const keyword = this.query().trim();
    if (!keyword) {
      this.error.set('Enter a page name to search, for example Gul_Creations.');
      this.pages.set([]);
      this.selectedId.set(null);
      this.searched.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.searched.set(true);
    this.selectedId.set(null);

    this.processApi.searchPublicPages(this.processRoute.currentMenuType(), keyword).subscribe({
      next: (res: ApiResponse<PublicPage[]>) => {
        this.loading.set(false);
        if (!res.success) {
          this.pages.set([]);
          this.selectedId.set(null);
          this.error.set(res.message || 'Page search failed.');
          return;
        }
        const pages = res.data ?? [];
        this.pages.set(pages);
        this.selectedId.set(pages[0] ? this.pageKey(pages[0]) : null);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.pages.set([]);
        this.selectedId.set(null);
        this.error.set(err?.error?.message || 'Page search failed.');
      }
    });
  }

  selectPage(page: PublicPage): void {
    this.selectedId.set(this.pageKey(page));
  }

  pageKey(page: PublicPage): string {
    return page.id || page.link || page.name;
  }

  initials(name: string): string {
    const parts = name.replace(/[_./-]+/g, ' ').trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return 'P';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  onQueryInput(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }
}
