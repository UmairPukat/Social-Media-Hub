import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../core/services/api.service';
import { SocialPlatform } from '../../core/models/models';

@Component({
  selector: 'app-create-post',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './create-post.component.html',
  styleUrl: './create-post.component.scss'
})
export class CreatePostComponent {
  readonly SocialPlatform = SocialPlatform;
  platform = SocialPlatform.Facebook;
  content = '';
  mediaUrl = '';
  publishing = false;

  readonly platformName = computed(() => SocialPlatform[this.platform]);

  constructor(private api: ApiService, private snack: MatSnackBar) {}

  onPlatformChange(value: SocialPlatform): void {
    this.platform = value;
  }

  publish(): void {
    if (!this.content.trim()) {
      this.snack.open('Write something before publishing.', 'Close', { duration: 2500 });
      return;
    }

    if (this.platform === SocialPlatform.WhatsApp) {
      this.snack.open('WhatsApp does not support feed posts. Use Inbox → Messages.', 'Close', { duration: 3500 });
      return;
    }

    this.publishing = true;
    this.api
      .createPost({
        platform: this.platform,
        content: this.content,
        mediaUrl: this.mediaUrl || undefined,
        publishNow: true
      })
      .subscribe({
        next: (res) => {
          this.publishing = false;
          this.snack.open(res.message || 'Post published.', 'Close', { duration: 3000 });
          this.content = '';
          this.mediaUrl = '';
        },
        error: (err) => {
          this.publishing = false;
          this.snack.open(err?.error?.message || 'Publish failed.', 'Close', { duration: 3500 });
        }
      });
  }
}
