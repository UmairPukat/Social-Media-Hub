import { ApplicationConfig, ErrorHandler, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { isChunkLoadError, reloadOnceOnStaleChunk } from './core/chunk-load-recovery';

class ChunkLoadErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    const nested = error && typeof error === 'object' && 'rejection' in error
      ? (error as { rejection: unknown }).rejection
      : undefined;
    if (isChunkLoadError(error) || isChunkLoadError(nested)) {
      reloadOnceOnStaleChunk();
      return;
    }
    console.error(error);
  }
}

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: ErrorHandler, useClass: ChunkLoadErrorHandler },
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync()
  ]
};
