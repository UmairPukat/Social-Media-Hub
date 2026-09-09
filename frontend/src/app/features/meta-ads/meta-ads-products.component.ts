import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaProduct } from '../../core/models/meta-ads.models';
import { metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-products',
  standalone: true,
  imports: [FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './meta-ads-products.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsProductsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly products = signal<MetaProduct[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly search = signal('');
  readonly nextCursor = signal<string | null>(null);

  readonly showForm = signal(false);
  readonly editProductId = signal('');
  readonly name = signal('App Review Demo Product');
  readonly retailerId = signal('');
  readonly price = signal('9.99');
  readonly currency = signal('USD');
  readonly availability = signal('in stock');
  readonly url = signal('');
  readonly imageUrl = signal('');

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.products().filter(p => !term || p.name.toLowerCase().includes(term) || (p.retailerId || '').includes(term));
  });

  ngOnInit(): void {
    this.load();
  }

  load(after?: string): void {
    const catalog = this.state.selectedCatalog();
    if (!catalog) {
      this.products.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.api.getProducts(this.processRoute.currentMenuType(), catalog.id, this.search(), after).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.products.set(res.data?.items ?? []);
        this.nextCursor.set(res.data?.nextCursor ?? null);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load products.');
      }
    });
  }

  openCreate(): void {
    this.editProductId.set('');
    this.retailerId.set(`demo-${Date.now()}`);
    this.showForm.set(true);
  }

  openEdit(product: MetaProduct): void {
    this.editProductId.set(product.id);
    this.name.set(product.name);
    this.retailerId.set(product.retailerId || product.id);
    this.price.set(product.price || '9.99');
    this.currency.set(product.currency || 'USD');
    this.availability.set(product.availability || 'in stock');
    this.url.set(product.url || '');
    this.imageUrl.set(product.imageUrl || '');
    this.showForm.set(true);
  }

  saveProduct(): void {
    const catalog = this.state.selectedCatalog();
    if (!catalog) return;

    this.saving.set(true);
    this.error.set('');

    const productId = this.editProductId();
    const request = productId
      ? this.api.updateProduct(this.processRoute.currentMenuType(), catalog.id, productId, {
          name: this.name().trim(),
          price: this.price(),
          currency: this.currency(),
          availability: this.availability(),
          url: this.url() || undefined,
          imageUrl: this.imageUrl() || undefined
        })
      : this.api.createProduct(this.processRoute.currentMenuType(), catalog.id, {
          catalogId: catalog.id,
          name: this.name().trim(),
          retailerId: this.retailerId().trim(),
          price: this.price(),
          currency: this.currency(),
          availability: this.availability(),
          url: this.url() || undefined,
          imageUrl: this.imageUrl() || undefined
        });

    request.subscribe({
      next: (res) => {
        this.saving.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set(productId ? 'Product updated.' : `Product created. ID: ${res.data?.id}`);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Unable to save product.');
      }
    });
  }

  deleteProduct(product: MetaProduct): void {
    const catalog = this.state.selectedCatalog();
    if (!catalog || !confirm(`Delete product "${product.name}"?`)) return;

    this.api.deleteProduct(this.processRoute.currentMenuType(), catalog.id, product.id).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set('Product deleted.');
        this.load();
      },
      error: () => this.error.set('Unable to delete product.')
    });
  }
}
