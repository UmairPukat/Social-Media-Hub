import { Injectable, signal } from '@angular/core';

const AD_ACCOUNT_KEY = 'meta-ads:adAccount';
const CATALOG_KEY = 'meta-ads:catalog';

export interface SelectedMetaAdAccount {
  id: string;
  name: string;
  currency?: string;
}

export interface SelectedMetaCatalog {
  id: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class MetaAdsStateService {
  readonly selectedAdAccount = signal<SelectedMetaAdAccount | null>(this.readAdAccount());
  readonly selectedCatalog = signal<SelectedMetaCatalog | null>(this.readCatalog());

  selectAdAccount(account: SelectedMetaAdAccount | null): void {
    if (account) {
      localStorage.setItem(AD_ACCOUNT_KEY, JSON.stringify(account));
    } else {
      localStorage.removeItem(AD_ACCOUNT_KEY);
    }
    this.selectedAdAccount.set(account);
  }

  selectCatalog(catalog: SelectedMetaCatalog | null): void {
    if (catalog) {
      localStorage.setItem(CATALOG_KEY, JSON.stringify(catalog));
    } else {
      localStorage.removeItem(CATALOG_KEY);
    }
    this.selectedCatalog.set(catalog);
  }

  private readAdAccount(): SelectedMetaAdAccount | null {
    try {
      const raw = localStorage.getItem(AD_ACCOUNT_KEY);
      return raw ? (JSON.parse(raw) as SelectedMetaAdAccount) : null;
    } catch {
      return null;
    }
  }

  private readCatalog(): SelectedMetaCatalog | null {
    try {
      const raw = localStorage.getItem(CATALOG_KEY);
      return raw ? (JSON.parse(raw) as SelectedMetaCatalog) : null;
    } catch {
      return null;
    }
  }
}
