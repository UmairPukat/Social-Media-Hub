import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api.models';
import { PROCESS_MODULE_LIST } from '../../core/config/process.config';

interface PurgeResult {
  menuType: string;
  label: string;
  deletedRows: number;
}

@Component({
  selector: 'app-clear-data',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './clear-data.component.html',
  styleUrl: './clear-data.component.scss'
})
export class ClearDataComponent {
  private readonly http = inject(HttpClient);

  readonly modules = PROCESS_MODULE_LIST;
  readonly busy = signal<string | null>(null);
  readonly message = signal('');
  readonly error = signal('');

  clearModule(menuType: string, label: string): void {
    const confirmed = window.confirm(
      `Permanently delete all ${label} posts, comments, messages, attachments, conversations, media, sync jobs, webhook records, and connected accounts? This cannot be undone.`
    );
    if (!confirmed) return;
    this.purge(menuType);
  }

  clearAll(): void {
    const confirmed = window.confirm(
      'Permanently delete posts, comments, messages, attachments, connected accounts, and related records for every module? This cannot be undone.'
    );
    if (!confirmed) return;
    this.purge('all');
  }

  private purge(menuType: string): void {
    this.busy.set(menuType);
    this.message.set('');
    this.error.set('');
    this.http
      .post<ApiResponse<PurgeResult>>(`${environment.apiUrl}/data-cleanup/${menuType}`, {})
      .subscribe({
        next: (response) => {
          this.busy.set(null);
          if (response.success) this.message.set(response.message);
          else this.error.set(response.message || 'Delete failed.');
        },
        error: () => {
          this.busy.set(null);
          this.error.set('Delete failed. You need an admin account, and the API must be running.');
        }
      });
  }
}
