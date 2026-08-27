import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PROCESS_MODULES, ProcessMenuType, processFromRoute } from '../config/process.config';

@Injectable({ providedIn: 'root' })
export class ProcessRouteService {
  private readonly router = inject(Router);

  currentMenuType(): ProcessMenuType {
    const module = processFromRoute(this.router.url);
    return module?.id ?? PROCESS_MODULES.integrations.id;
  }

  currentRouteBase(): string {
    const module = processFromRoute(this.router.url);
    return module?.routeBase ?? PROCESS_MODULES.integrations.routeBase;
  }
}
