import { inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ProcessRouteService } from '../../core/services/process-route.service';

/** Reloads a Meta Ads screen when the open module changes. Call from the component constructor. */
export function watchMetaAdsMenu(reload: () => void): void {
  const router = inject(Router);
  const processRoute = inject(ProcessRouteService);
  let menu = processRoute.currentMenuType();

  router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    takeUntilDestroyed()
  ).subscribe(() => {
    if (!router.url.includes('/meta-ads')) return;
    const next = processRoute.currentMenuType();
    if (next === menu) return;
    menu = next;
    reload();
  });
}
