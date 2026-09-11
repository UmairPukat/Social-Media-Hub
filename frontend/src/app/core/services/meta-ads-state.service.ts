import { Injectable, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ProcessMenuType } from '../config/process.config';
import { ProcessRouteService } from './process-route.service';

export interface SelectedMetaAdAccount {
  id: string;
  name: string;
  currency?: string;
}

export interface SelectedMetaCatalog {
  id: string;
  name: string;
  businessId?: string;
}

@Injectable({ providedIn: 'root' })
export class MetaAdsStateService {
  private readonly router = inject(Router);
  private readonly processRoute = inject(ProcessRouteService);

  readonly selectedAdAccount = signal<SelectedMetaAdAccount | null>(null);
  readonly selectedCatalog = signal<SelectedMetaCatalog | null>(null);

  constructor() {
    this.syncForCurrentProcess();
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.syncForCurrentProcess();
    });
  }

  selectAdAccount(account: SelectedMetaAdAccount | null, menuType?: ProcessMenuType): void {
    const scope = menuType ?? this.processRoute.currentMenuType();
    const key = this.adAccountKey(scope);
    if (account) {
      localStorage.setItem(key, JSON.stringify(account));
    } else {
      localStorage.removeItem(key);
    }
    if (scope === this.processRoute.currentMenuType()) {
      this.selectedAdAccount.set(account);
    }
  }

  selectCatalog(catalog: SelectedMetaCatalog | null, menuType?: ProcessMenuType): void {
    const scope = menuType ?? this.processRoute.currentMenuType();
    const key = this.catalogKey(scope);
    if (catalog) {
      localStorage.setItem(key, JSON.stringify(catalog));
    } else {
      localStorage.removeItem(key);
    }
    if (scope === this.processRoute.currentMenuType()) {
      this.selectedCatalog.set(catalog);
    }
  }

  syncForCurrentProcess(): void {
    const menuType = this.processRoute.currentMenuType();
    this.selectedAdAccount.set(this.readAdAccount(menuType));
    this.selectedCatalog.set(this.readCatalog(menuType));
  }

  private adAccountKey(menuType: ProcessMenuType): string {
    return `meta-ads:${menuType}:adAccount`;
  }

  private catalogKey(menuType: ProcessMenuType): string {
    return `meta-ads:${menuType}:catalog`;
  }

  private readAdAccount(menuType: ProcessMenuType): SelectedMetaAdAccount | null {
    try {
      const raw = localStorage.getItem(this.adAccountKey(menuType));
      return raw ? (JSON.parse(raw) as SelectedMetaAdAccount) : null;
    } catch {
      return null;
    }
  }

  private readCatalog(menuType: ProcessMenuType): SelectedMetaCatalog | null {
    try {
      const raw = localStorage.getItem(this.catalogKey(menuType));
      return raw ? (JSON.parse(raw) as SelectedMetaCatalog) : null;
    } catch {
      return null;
    }
  }
}
