import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs/operators';
import { ApiService } from '../../core/services/api.service';
import {
  ApiResponse,
  EnvironmentVariable,
  EnvironmentVariableScope,
  UpsertEnvironmentVariableRequest
} from '../../core/models/api.models';

interface EnvFormModel {
  name: string;
  value: string;
  description: string;
  isRequired: boolean;
}

@Component({
  selector: 'app-environment-variables',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './environment-variables.component.html',
  styleUrl: './environment-variables.component.scss'
})
export class EnvironmentVariablesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly formDialog = viewChild<ElementRef<HTMLDialogElement>>('formDialog');
  private readonly deleteDialog = viewChild<ElementRef<HTMLDialogElement>>('deleteDialog');

  readonly scope = signal<EnvironmentVariableScope>('frontend');
  readonly variables = signal<EnvironmentVariable[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly notice = signal('');
  readonly search = signal('');
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly formError = signal('');
  readonly editingId = signal<string | null>(null);
  readonly pendingDelete = signal<EnvironmentVariable | null>(null);
  readonly revealedIds = signal<Record<string, string>>({});
  readonly revealingId = signal<string | null>(null);
  readonly copiedKey = signal<string | null>(null);

  readonly form = signal<EnvFormModel>({
    name: '',
    value: '',
    description: '',
    isRequired: false
  });

  readonly pageMeta = computed(() => {
    const scope = this.scope();
    return scope === 'frontend'
      ? {
          title: 'Frontend Environment Variables',
          eyebrow: 'Deployment config',
          lede: 'Manage build-time variables for the Angular app on Railway, Vercel, or any hosting provider.',
          icon: 'web'
        }
      : {
          title: 'Backend Environment Variables',
          eyebrow: 'Deployment config',
          lede: 'Manage runtime variables for the ASP.NET Core API, database, JWT, and Meta integrations.',
          icon: 'dns'
        };
  });

  readonly filteredVariables = computed(() => {
    const query = this.search().trim().toLowerCase();
    if (!query) return this.variables();
    return this.variables().filter((item) => {
      const haystack = [item.name, item.description, item.isRequired ? 'required' : 'optional']
        .join(' ')
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const scope = params.get('scope');
      if (scope === 'frontend' || scope === 'backend') {
        this.scope.set(scope);
        this.revealedIds.set({});
        this.reload();
        return;
      }
      void this.router.navigate(['/app/environment-variables/frontend']);
    });
  }

  reload(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.api
      .getEnvironmentVariables(this.scope())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response: ApiResponse<EnvironmentVariable[]>) => {
          if (!response.success) {
            this.loadError.set(response.message || 'Could not load environment variables.');
            return;
          }
          this.variables.set(response.data || []);
        },
        error: () =>
          this.loadError.set('We could not load environment variables. Check the API connection and try again.')
      });
  }

  setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  clearSearch(): void {
    this.search.set('');
  }

  displayValue(item: EnvironmentVariable): string {
    const revealed = this.revealedIds()[item.id];
    if (revealed !== undefined) return revealed;
    return item.value;
  }

  isValueVisible(item: EnvironmentVariable): boolean {
    return !item.isSensitive || this.revealedIds()[item.id] !== undefined;
  }

  toggleVisibility(item: EnvironmentVariable): void {
    if (!item.isSensitive) return;

    if (this.revealedIds()[item.id] !== undefined) {
      this.revealedIds.update((state) => {
        const next = { ...state };
        delete next[item.id];
        return next;
      });
      return;
    }

    this.revealingId.set(item.id);
    this.api
      .getEnvironmentVariable(item.id, true)
      .pipe(finalize(() => this.revealingId.set(null)))
      .subscribe({
        next: (response) => {
          if (!response.success || !response.data) {
            this.notice.set(response.message || 'Could not reveal this value.');
            return;
          }
          this.revealedIds.update((state) => ({ ...state, [item.id]: response.data!.value }));
        },
        error: () => this.notice.set('Could not reveal this value.')
      });
  }

  async copyValue(item: EnvironmentVariable, kind: 'name' | 'value'): Promise<void> {
    const key = `${item.id}:${kind}`;
    let text = kind === 'name' ? item.name : this.displayValue(item);

    if (kind === 'value' && item.isSensitive && !this.isValueVisible(item)) {
      this.revealingId.set(item.id);
      this.api
        .getEnvironmentVariable(item.id, true)
        .pipe(finalize(() => this.revealingId.set(null)))
        .subscribe({
          next: async (response) => {
            if (!response.success || !response.data) {
              this.notice.set(response.message || 'Could not copy this value.');
              return;
            }
            this.revealedIds.update((state) => ({ ...state, [item.id]: response.data!.value }));
            await this.writeClipboard(response.data.value, key);
          },
          error: () => this.notice.set('Could not copy this value.')
        });
      return;
    }

    await this.writeClipboard(text, key);
  }

  openCreate(): void {
    this.editingId.set(null);
    this.formError.set('');
    this.form.set({ name: '', value: '', description: '', isRequired: false });
    this.openFormDialog();
  }

  openEdit(item: EnvironmentVariable): void {
    this.editingId.set(item.id);
    this.formError.set('');

    const applyForm = (value: string) => {
      this.form.set({
        name: item.name,
        value,
        description: item.description,
        isRequired: item.isRequired
      });
      this.openFormDialog();
    };

    if (item.isSensitive && item.isMasked) {
      this.revealingId.set(item.id);
      this.api
        .getEnvironmentVariable(item.id, true)
        .pipe(finalize(() => this.revealingId.set(null)))
        .subscribe({
          next: (response) => {
            if (!response.success || !response.data) {
              this.notice.set(response.message || 'Could not load variable for editing.');
              return;
            }
            applyForm(response.data.value);
          },
          error: () => this.notice.set('Could not load variable for editing.')
        });
      return;
    }

    applyForm(item.value);
  }

  closeFormDialog(): void {
    this.formDialog()?.nativeElement.close();
  }

  saveForm(): void {
    const model = this.form();
    const name = model.name.trim();
    if (!name) {
      this.formError.set('Variable name is required.');
      return;
    }

    const duplicate = this.variables().some(
      (item) => item.name.toLowerCase() === name.toLowerCase() && item.id !== this.editingId()
    );
    if (duplicate) {
      this.formError.set('A variable with this name already exists.');
      return;
    }

    const body: UpsertEnvironmentVariableRequest = {
      name,
      value: model.value,
      description: model.description.trim(),
      isRequired: model.isRequired,
      scope: this.scope()
    };

    this.saving.set(true);
    this.formError.set('');

    const request$ = this.editingId()
      ? this.api.updateEnvironmentVariable(this.editingId()!, body)
      : this.api.createEnvironmentVariable(body);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (response) => {
        if (!response.success || !response.data) {
          this.formError.set(response.message || 'Could not save the variable.');
          return;
        }
        this.closeFormDialog();
        this.notice.set(this.editingId() ? 'Variable updated.' : 'Variable added.');
        this.reload();
      },
      error: () => this.formError.set('Could not save the variable. Please try again.')
    });
  }

  requestDelete(item: EnvironmentVariable): void {
    this.pendingDelete.set(item);
    this.deleteDialog()?.nativeElement.showModal();
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
    this.deleteDialog()?.nativeElement.close();
  }

  confirmDelete(): void {
    const item = this.pendingDelete();
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.api
      .deleteEnvironmentVariable(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: (response) => {
          if (!response.success) {
            this.notice.set(response.message || 'Could not delete the variable.');
            return;
          }
          this.variables.update((items) => items.filter((entry) => entry.id !== item.id));
          this.revealedIds.update((state) => {
            const next = { ...state };
            delete next[item.id];
            return next;
          });
          this.notice.set('Variable deleted.');
          this.cancelDelete();
        },
        error: () => this.notice.set('Could not delete the variable. Please try again.')
      });
  }

  updateFormField<K extends keyof EnvFormModel>(field: K, value: EnvFormModel[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  private openFormDialog(): void {
    this.formDialog()?.nativeElement.showModal();
  }

  private async writeClipboard(text: string, key: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copiedKey.set(key);
      window.setTimeout(() => {
        if (this.copiedKey() === key) this.copiedKey.set(null);
      }, 1800);
    } catch {
      this.notice.set('Clipboard access was blocked by the browser.');
    }
  }
}
