import { Injectable, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, filter, map } from 'rxjs/operators';
import { ProcessMenuType } from '../config/process.config';
import { MetaApiResponse, MetaAdAccount } from '../models/meta-ads.models';
import { MetaAdsApiService } from './meta-ads-api.service';
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
  private readonly api = inject(MetaAdsApiService);

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

  /**
   * Uses this module's saved ad account, or loads ad accounts from this module's API
   * and selects the same Meta ad account already used in another module when that
   * account is in the list. Otherwise selects the first account.
   */
  ensureAdAccount(): Observable<{ account: SelectedMetaAdAccount | null; error: string }> {
    this.syncForCurrentProcess();
    const menuType = this.processRoute.currentMenuType();
    const existing = this.selectedAdAccount();
    if (existing?.id) return of({ account: existing, error: '' });

    return this.api.getAdAccounts(menuType).pipe(
      map(response => {
        if (!response.success) return { account: null, error: this.readError(response) };
        const selected = this.selectBestAccount(response.data ?? [], menuType);
        return { account: selected, error: '' };
      }),
      catchError(() => of({ account: null, error: 'Unable to load ad accounts.' }))
    );
  }

  private adAccountKey(menuType: ProcessMenuType): string {
    return `meta-ads:${menuType}:adAccount`;
  }

  private catalogKey(menuType: ProcessMenuType): string {
    return `meta-ads:${menuType}:catalog`;
  }

  selectBestAccount(accounts: MetaAdAccount[], menuType?: ProcessMenuType): SelectedMetaAdAccount | null {
    const chosen = this.pickAccount(accounts);
    if (!chosen) return null;
    const selected: SelectedMetaAdAccount = {
      id: chosen.id,
      name: chosen.name,
      currency: chosen.currency
    };
    this.selectAdAccount(selected, menuType);
    return selected;
  }

  private pickAccount(accounts: MetaAdAccount[]): MetaAdAccount | null {
    if (!accounts.length) return null;
    for (const id of this.savedAdAccountIds()) {
      const match = accounts.find(account => account.id === id);
      if (match) return match;
    }
    return accounts[0];
  }

  private savedAdAccountIds(): string[] {
    const ids: string[] = [];
    const push = (key: string | null) => {
      if (!key) return;
      try {
        const parsed = JSON.parse(localStorage.getItem(key) || '') as SelectedMetaAdAccount;
        if (parsed?.id && !ids.includes(parsed.id)) ids.push(parsed.id);
      } catch {
        return;
      }
    };

    push('meta-ads:integration:adAccount');
    for (let index = 0; index < localStorage.length; index += 1) {
      const key = localStorage.key(index);
      if (key?.startsWith('meta-ads:') && key.endsWith(':adAccount')) push(key);
    }
    return ids;
  }

  private readError(response: MetaApiResponse<MetaAdAccount[]>): string {
    if (response.metaErrorMessage) {
      return `${response.message}${response.metaErrorCode ? ` (${response.metaErrorCode})` : ''}: ${response.metaErrorMessage}`;
    }
    return response.message || 'Unable to load ad accounts.';
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
