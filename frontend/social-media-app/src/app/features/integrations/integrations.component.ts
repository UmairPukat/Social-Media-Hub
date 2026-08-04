import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/services/api.service';
import { PlatformCard, SocialPlatform } from '../../core/models/models';

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatSnackBarModule, MatProgressSpinnerModule],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {
  readonly platforms = signal<PlatformCard[]>([]);
  readonly loading = signal(true);

  constructor(private api: ApiService, private snack: MatSnackBar) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.getPlatforms().subscribe({
      next: (res) => {
        this.platforms.set(res.data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snack.open('Failed to load integrations.', 'Close', { duration: 3000 });
      }
    });
  }

  color(icon: string): string {
    const map: Record<string, string> = {
      facebook: 'var(--fb)',
      instagram: 'var(--ig)',
      whatsapp: 'var(--wa)',
      youtube: 'var(--yt)',
      linkedin: 'var(--li)',
      tiktok: 'var(--tt)',
      twitter: '#111',
      other: '#607d8b'
    };
    return map[icon] || '#607d8b';
  }

  connect(card: PlatformCard): void {
    if (!card.isImplemented) {
      this.snack.open(`${card.name} is coming soon.`, 'Close', { duration: 2500 });
      return;
    }

    const key =
      card.platform === SocialPlatform.Facebook
        ? 'facebook'
        : card.platform === SocialPlatform.Instagram
          ? 'instagram'
          : card.platform === SocialPlatform.WhatsApp
            ? 'whatsapp'
            : null;

    if (!key) return;

    this.api.getAuthUrl(key).subscribe({
      next: (res) => {
        window.location.href = res.data.authorizationUrl;
      },
      error: () => this.snack.open('Unable to start Meta auth. Check Meta App settings.', 'Close', { duration: 3500 })
    });
  }

  disconnect(card: PlatformCard): void {
    this.api.disconnect(card.platform).subscribe({
      next: () => {
        this.snack.open(`${card.name} disconnected.`, 'Close', { duration: 2500 });
        this.reload();
      },
      error: () => this.snack.open('Disconnect failed.', 'Close', { duration: 2500 })
    });
  }
}
