import { Injectable, inject } from '@angular/core';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { PROCESS_MODULE_LIST, PROCESS_MODULES, ProcessMenuType, processFromRoute } from '../config/process.config';

@Injectable({ providedIn: 'root' })
export class ProcessRouteService {
  private readonly router = inject(Router);

  currentMenuType(): ProcessMenuType {
    const fromRoute = this.menuFromRouterState();
    if (fromRoute) return fromRoute;
    const module = processFromRoute(this.router.url);
    return module?.id ?? PROCESS_MODULES.integrations.id;
  }

  currentRouteBase(): string {
    const menuType = this.menuFromRouterState();
    const fromData = PROCESS_MODULE_LIST.find(module => module.id === menuType);
    if (fromData) return fromData.routeBase;
    const module = processFromRoute(this.router.url);
    return module?.routeBase ?? PROCESS_MODULES.integrations.routeBase;
  }

  private menuFromRouterState(): ProcessMenuType | null {
    let route: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    let found: ProcessMenuType | null = null;
    while (route) {
      const menu = route.data?.['processMenu'] as ProcessMenuType | undefined;
      if (menu) found = menu;
      route = route.firstChild;
    }
    return found;
  }
}
