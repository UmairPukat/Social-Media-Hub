import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../core/services/api.service';
import { InboxItem, InboxItemType, SocialPlatform } from '../../core/models/models';

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTabsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSnackBarModule
  ],
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.scss'
})
export class InboxComponent implements OnInit {
  readonly SocialPlatform = SocialPlatform;
  readonly InboxItemType = InboxItemType;

  platformFilter: SocialPlatform | null = null;
  selectedTab = 0;
  replyText = '';
  selected: InboxItem | null = null;

  readonly items = signal<InboxItem[]>([]);
  readonly loading = signal(false);

  /** WhatsApp has messages only — hide Comments tab when filtered to WhatsApp. */
  readonly showCommentsTab = computed(() => this.platformFilter !== SocialPlatform.WhatsApp);

  constructor(private api: ApiService, private snack: MatSnackBar) {}

  ngOnInit(): void {
    this.load();
  }

  onPlatformChange(value: SocialPlatform | null): void {
    this.platformFilter = value;
    if (value === SocialPlatform.WhatsApp) {
      this.selectedTab = 0; // messages-only view
    }
    this.load();
  }

  onTabChange(index: number): void {
    this.selectedTab = index;
    this.selected = null;
    this.load();
  }

  load(): void {
    this.loading.set(true);

    let itemType: InboxItemType | null = null;
    if (this.platformFilter === SocialPlatform.WhatsApp) {
      itemType = InboxItemType.Message;
    } else {
      // Tab 0 = Comments, Tab 1 = Messages when comments are available
      itemType = this.showCommentsTab()
        ? this.selectedTab === 0
          ? InboxItemType.Comment
          : InboxItemType.Message
        : InboxItemType.Message;
    }

    this.api.getInbox(this.platformFilter, itemType).subscribe({
      next: (res) => {
        this.items.set(res.data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snack.open('Failed to load inbox.', 'Close', { duration: 3000 });
      }
    });
  }

  select(item: InboxItem): void {
    this.selected = item;
  }

  sendReply(): void {
    if (!this.selected || !this.replyText.trim()) return;

    this.api.reply(this.selected.id, this.replyText.trim()).subscribe({
      next: () => {
        this.snack.open('Reply sent.', 'Close', { duration: 2500 });
        this.replyText = '';
      },
      error: (err) => this.snack.open(err?.error?.message || 'Reply failed.', 'Close', { duration: 3000 })
    });
  }

  hide(): void {
    if (!this.selected) return;
    this.api.hideComment(this.selected.id).subscribe({
      next: () => {
        this.snack.open('Comment hidden.', 'Close', { duration: 2500 });
        this.load();
      },
      error: (err) => this.snack.open(err?.error?.message || 'Hide failed.', 'Close', { duration: 3000 })
    });
  }

  remove(): void {
    if (!this.selected) return;
    this.api.deleteInboxItem(this.selected.id).subscribe({
      next: () => {
        this.snack.open('Deleted.', 'Close', { duration: 2500 });
        this.selected = null;
        this.load();
      },
      error: (err) => this.snack.open(err?.error?.message || 'Delete failed.', 'Close', { duration: 3000 })
    });
  }
}
