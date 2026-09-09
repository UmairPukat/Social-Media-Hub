import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { UsersService } from '../../core/services/users.service';
import { UserListItem } from '../../core/models/api.models';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule
  ],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss'
})
export class UsersComponent implements OnInit {
  private readonly usersApi = inject(UsersService);
  private readonly resetDialog = viewChild<ElementRef<HTMLDialogElement>>('resetDialog');

  readonly users = signal<UserListItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly banner = signal('');
  readonly selectedUser = signal<UserListItem | null>(null);
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly showPassword = signal(false);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.usersApi.list().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(res.message || 'Could not load users.');
          return;
        }
        this.users.set(res.data || []);
      },
      error: (err: { error?: { message?: string } }) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Could not load users.');
      }
    });
  }

  openReset(user: UserListItem): void {
    this.selectedUser.set(user);
    this.newPassword.set('');
    this.confirmPassword.set('');
    this.showPassword.set(false);
    this.error.set('');
    const dialog = this.resetDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();
  }

  closeReset(): void {
    const dialog = this.resetDialog()?.nativeElement;
    if (dialog?.open) dialog.close();
    this.selectedUser.set(null);
    this.newPassword.set('');
    this.confirmPassword.set('');
  }

  submitReset(): void {
    const user = this.selectedUser();
    if (!user) return;

    const password = this.newPassword().trim();
    const confirm = this.confirmPassword().trim();

    if (password.length < 6) {
      this.error.set('Password must be at least 6 characters.');
      return;
    }

    if (password !== confirm) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.usersApi.resetPassword(user.id, { newPassword: password }).subscribe({
      next: (res) => {
        this.saving.set(false);
        if (!res.success) {
          this.error.set(res.message || 'Could not reset password.');
          return;
        }
        this.banner.set(`Password updated for ${user.email}.`);
        this.closeReset();
      },
      error: (err: { error?: { message?: string } }) => {
        this.saving.set(false);
        this.error.set(err?.error?.message || 'Could not reset password.');
      }
    });
  }

  deleteUser(user: UserListItem): void {
    const confirmed = confirm(`Delete ${user.fullName} (${user.email})? This cannot be undone.`);
    if (!confirmed) return;

    this.usersApi.delete(user.id).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(res.message || 'Could not delete user.');
          return;
        }
        this.banner.set(`Deleted ${user.email}.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) => {
        this.error.set(err?.error?.message || 'Could not delete user.');
      }
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }
}
