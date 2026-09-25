import { Injectable, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MetaApiResponse, MetaUsageSnapshot } from '../models/meta-ads.models';

const STORAGE_KEY = 'socialhub_meta_api_calls';
const MAX_LOG_ENTRIES = 2000;

export interface MetaApiCallLogEntry {
  timestamp: string;
  method: string;
  path: string;
  success: boolean;
  statusCode?: number;
  errorCode?: string;
  usagePercent?: number;
}

@Injectable({ providedIn: 'root' })
export class MetaRateLimitService {
  private readonly snackBar = inject(MatSnackBar);
  private lastWarningAt = 0;

  readonly bucUsagePercent = signal(0);
  readonly adAccountUsagePercent = signal(0);
  readonly appUsagePercent = signal(0);

  handleResponse<T>(response: MetaApiResponse<T>, context: { method: string; path: string; success: boolean }): void {
    this.logCall({
      timestamp: new Date().toISOString(),
      method: context.method,
      path: context.path,
      success: context.success,
      errorCode: response.metaErrorCode,
      usagePercent: response.metaUsage?.overallPercent
    });

    if (!response.metaUsage) return;

    const usage = response.metaUsage;
    this.bucUsagePercent.set(usage.businessUseCasePercent);
    this.adAccountUsagePercent.set(usage.adAccountPercent);
    this.appUsagePercent.set(usage.appPercent);

    if (usage.isApproachingLimit || usage.overallPercent >= 80) {
      this.showRateLimitWarning(usage.overallPercent);
    }
  }

  readLog(): MetaApiCallLogEntry[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as MetaApiCallLogEntry[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  clearLog(): void {
    localStorage.removeItem(STORAGE_KEY);
  }

  private logCall(entry: MetaApiCallLogEntry): void {
    const log = this.readLog();
    log.unshift(entry);
    if (log.length > MAX_LOG_ENTRIES) {
      log.length = MAX_LOG_ENTRIES;
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(log));
  }

  private showRateLimitWarning(percent: number): void {
    const now = Date.now();
    if (now - this.lastWarningAt < 60_000) return;
    this.lastWarningAt = now;

    this.snackBar.open(`Approaching rate limit (${percent}% API usage)`, 'Dismiss', {
      duration: 8000,
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
      panelClass: ['meta-rate-limit-toast']
    });
  }
}
