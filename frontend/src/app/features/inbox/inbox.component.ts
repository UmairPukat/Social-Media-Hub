import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { InboxRealtimeService } from '../../core/services/inbox-realtime.service';
import {
  ApiResponse,
  InboxItem,
  InboxPostMeta,
  PLATFORM_COLORS
} from '../../core/models/api.models';

export interface MessageConversation {
  key: string;
  authorName: string;
  authorId?: string;
  platformCode: string;
  lastContent: string;
  lastAt: string;
  unreadCount: number;
  items: InboxItem[];
}

export interface CommentPostThread {
  key: string;
  platformCode: string;
  post: InboxPostMeta;
  comments: InboxItem[];
  lastAt: string;
  unreadCount: number;
}

export interface ThreadBubble {
  id: string;
  content: string;
  at: string;
  outgoing: boolean;
}

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.scss'
})
export class InboxComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly realtime = inject(InboxRealtimeService);
  private realtimeSub?: Subscription;

  readonly items = signal<InboxItem[]>([]);
  readonly platformCode = signal<string | null>(null);
  readonly mode = signal<'messages' | 'comments'>('comments');
  readonly selectedKey = signal<string | null>(null);
  readonly listQuery = signal('');
  readonly replyText = signal('');
  readonly replyTargetCommentId = signal<string | null>(null);
  readonly replyTargetAuthor = signal<string | null>(null);
  readonly localOutgoing = signal<Record<string, ThreadBubble[]>>({});
  readonly banner = signal('');
  readonly sending = signal(false);
  readonly colors = PLATFORM_COLORS;

  readonly platformFilters = [
    { code: null as string | null, label: 'All', icon: 'apps' },
    { code: 'facebook', label: 'Facebook', icon: 'facebook' },
    { code: 'instagram', label: 'Instagram', icon: 'photo_camera' },
    { code: 'whatsapp', label: 'WhatsApp', icon: 'chat' }
  ];

  readonly showCommentsMode = computed(() => this.platformCode() !== 'whatsapp');

  readonly filteredItems = computed(() => {
    const code = this.platformCode();
    const kind = this.mode() === 'comments' ? 'comment' : 'message';
    return this.items().filter(i =>
      i.itemKind === kind &&
      (!code || i.platformCode === code)
    );
  });

  readonly messageConversations = computed(() => {
    const grouped = new Map<string, MessageConversation>();
    for (const item of this.filteredItems()) {
      if (item.itemKind !== 'message') continue;
      const key = `msg:${item.platformCode}:${item.conversationId || item.authorId || item.authorName}`;
      const existing = grouped.get(key);
      if (!existing) {
        grouped.set(key, {
          key,
          authorName: item.isOutgoing ? 'Instagram user' : item.authorName || 'Instagram user',
          authorId: item.authorId,
          platformCode: item.platformCode,
          lastContent: item.content,
          lastAt: item.receivedAt,
          unreadCount: item.isRead ? 0 : 1,
          items: [item]
        });
      } else {
        existing.items.push(item);
        if (!item.isOutgoing && item.authorName) {
          existing.authorName = item.authorName;
          existing.authorId = item.authorId;
        }
        if (!item.isRead) existing.unreadCount += 1;
        if (new Date(item.receivedAt) > new Date(existing.lastAt)) {
          existing.lastContent = item.content;
          existing.lastAt = item.receivedAt;
        }
      }
    }

    const q = this.listQuery().trim().toLowerCase();
    return [...grouped.values()]
      .filter(c =>
        !q ||
        c.authorName.toLowerCase().includes(q) ||
        c.lastContent.toLowerCase().includes(q)
      )
      .sort((a, b) => +new Date(b.lastAt) - +new Date(a.lastAt));
  });

  readonly commentThreads = computed(() => {
    const grouped = new Map<string, CommentPostThread>();
    for (const item of this.filteredItems()) {
      if (item.itemKind !== 'comment' || !item.post) continue;
      const key = `post:${item.platformCode}:${item.post.postId}`;
      const existing = grouped.get(key);
      if (!existing) {
        grouped.set(key, {
          key,
          platformCode: item.platformCode,
          post: item.post,
          comments: [item],
          lastAt: item.receivedAt,
          unreadCount: item.isRead ? 0 : 1
        });
      } else {
        existing.comments.push(item);
        if (!item.isRead) existing.unreadCount += 1;
        if (new Date(item.receivedAt) > new Date(existing.lastAt)) {
          existing.lastAt = item.receivedAt;
        }
      }
    }

    const q = this.listQuery().trim().toLowerCase();
    return [...grouped.values()]
      .filter(t =>
        !q ||
        t.post.pageName.toLowerCase().includes(q) ||
        t.post.postText.toLowerCase().includes(q) ||
        t.comments.some(c => c.content.toLowerCase().includes(q) || c.authorName.toLowerCase().includes(q))
      )
      .sort((a, b) => +new Date(b.lastAt) - +new Date(a.lastAt));
  });

  readonly selectedMessage = computed(() => {
    const key = this.selectedKey();
    if (!key?.startsWith('msg:')) return null;
    return this.messageConversations().find(c => c.key === key) ?? null;
  });

  readonly selectedCommentThread = computed(() => {
    const key = this.selectedKey();
    if (!key?.startsWith('post:')) return null;
    return this.commentThreads().find(t => t.key === key) ?? null;
  });

  readonly messageThread = computed((): ThreadBubble[] => {
    const conv = this.selectedMessage();
    if (!conv) return [];
    const incoming = [...conv.items]
      .sort((a, b) => +new Date(a.receivedAt) - +new Date(b.receivedAt))
      .map(i => ({ id: i.id, content: i.content, at: i.receivedAt, outgoing: !!i.isOutgoing }));
    const outgoing = this.localOutgoing()[conv.key] || [];
    return [...incoming, ...outgoing].sort((a, b) => +new Date(a.at) - +new Date(b.at));
  });

  ngOnInit(): void {
    this.reload();
    this.realtimeSub = this.realtime.item$.subscribe((item) => this.upsertItem(item));
    void this.realtime.start().catch(() => {
      this.banner.set('Live updates unavailable. Refresh Inbox to pull new webhook items.');
    });
  }

  ngOnDestroy(): void {
    this.realtimeSub?.unsubscribe();
    void this.realtime.stop();
  }

  private upsertItem(item: InboxItem): void {
    this.items.update((list) => {
      const existing = list.findIndex((row) => row.id === item.id || row.externalId === item.externalId);
      if (existing >= 0) {
        const next = [...list];
        next[existing] = { ...next[existing], ...item };
        return next;
      }
      return [item, ...list];
    });

    if (!this.selectedKey()) {
      this.autoSelectFirst();
    }
  }

  setPlatform(code: string | null): void {
    this.platformCode.set(code);
    if (code === 'whatsapp') this.mode.set('messages');
    this.selectedKey.set(null);
    queueMicrotask(() => this.autoSelectFirst());
  }

  setMode(mode: 'messages' | 'comments'): void {
    if (mode === 'comments' && !this.showCommentsMode()) return;
    this.mode.set(mode);
    this.selectedKey.set(null);
    queueMicrotask(() => this.autoSelectFirst());
  }

  reload(): void {
    this.api.getInbox().subscribe({
      next: (res: ApiResponse<InboxItem[]>) => {
        this.items.set(res.data || []);
        this.banner.set('');
        this.autoSelectFirst();
      },
      error: () => {
        this.items.set([]);
        this.banner.set('Inbox API is unavailable. Live Instagram data could not be loaded.');
        this.autoSelectFirst();
      }
    });
  }

  private autoSelectFirst(): void {
    if (this.selectedKey()) return;
    if (this.mode() === 'comments') {
      const first = this.commentThreads()[0];
      if (first) this.selectedKey.set(first.key);
    } else {
      const first = this.messageConversations()[0];
      if (first) this.selectedKey.set(first.key);
    }
  }

  selectMessage(conv: MessageConversation): void {
    this.selectedKey.set(conv.key);
    this.replyText.set('');
    const conversationId = conv.items.find((item) => item.conversationId)?.conversationId;
    if (conversationId && conv.unreadCount > 0) {
      this.api.markRead(conversationId).subscribe({
        next: () => this.items.update((items) =>
          items.map((item) => item.conversationId === conversationId ? { ...item, isRead: true } : item)
        )
      });
    }
  }

  selectCommentThread(thread: CommentPostThread): void {
    this.selectedKey.set(thread.key);
    this.replyText.set('');
    this.replyTargetCommentId.set(null);
    this.replyTargetAuthor.set(null);
  }

  beginCommentReply(comment: InboxItem): void {
    this.replyTargetCommentId.set(comment.id);
    this.replyTargetAuthor.set(comment.authorName || 'comment');
    this.replyText.set('');
    queueMicrotask(() => {
      const el = document.querySelector<HTMLTextAreaElement>('.composer.light textarea');
      el?.focus();
    });
  }

  clearCommentReplyTarget(): void {
    this.replyTargetCommentId.set(null);
    this.replyTargetAuthor.set(null);
  }

  initials(name: string): string {
    const parts = (name || '?').trim().split(/\s+/).slice(0, 2);
    return parts.map(p => p[0]?.toUpperCase() || '').join('') || '?';
  }

  color(code: string): string {
    return this.colors[code?.toLowerCase()] || '#64748B';
  }

  platformIcon(code: string): string {
    switch (code?.toLowerCase()) {
      case 'facebook': return 'facebook';
      case 'instagram': return 'photo_camera';
      case 'whatsapp': return 'chat';
      default: return 'public';
    }
  }

  formatCount(n: number): string {
    if (n >= 1000) return `${(n / 1000).toFixed(n >= 10000 ? 0 : 1)}K`;
    return String(n);
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault();
      this.sendReply();
    }
  }

  sendReply(): void {
    const text = this.replyText().trim();
    if (!text || this.sending()) return;

    if (this.mode() === 'messages') {
      const conv = this.selectedMessage();
      if (!conv) return;
      const latest = [...conv.items].sort((a, b) => +new Date(b.receivedAt) - +new Date(a.receivedAt))[0];
      if (!latest) return;
      this.sending.set(true);
      this.api.replyMessage(latest.id, text).subscribe({
        next: (res) => {
          if (res.success) {
            const messageId = (res.data as { messageId?: string } | null)?.messageId;
            this.upsertItem({
              id: messageId || `local-${Date.now()}`,
              itemKind: 'message',
              platformCode: conv.platformCode,
              externalId: messageId || `local_msg_${Date.now()}`,
              authorName: 'You',
              authorId: latest.authorId,
              content: text,
              isHidden: false,
              isRead: true,
              isOutgoing: true,
              conversationId: latest.conversationId,
              receivedAt: new Date().toISOString()
            });
            this.replyText.set('');
            this.banner.set('');
          } else {
            this.banner.set(res.message || 'Reply failed');
          }
          this.sending.set(false);
        },
        error: (err: { error?: { message?: string } }) => {
          this.banner.set(err?.error?.message || 'Reply failed');
          this.sending.set(false);
        }
      });
      return;
    }

    const thread = this.selectedCommentThread();
    if (!thread) return;

    const targetId = this.replyTargetCommentId();
    const target = targetId
      ? thread.comments.find(c => c.id === targetId)
      : [...thread.comments]
          .filter(c => !c.isOutgoing)
          .sort((a, b) => +new Date(b.receivedAt) - +new Date(a.receivedAt))[0]
        ?? [...thread.comments].sort((a, b) => +new Date(b.receivedAt) - +new Date(a.receivedAt))[0];

    if (!target) {
      this.banner.set('No comment available to reply to.');
      return;
    }

    this.sending.set(true);
    this.api.replyComment(target.id, text).subscribe({
      next: (res) => {
        if (res.success) {
          const replyId = (res.data as { replyId?: string } | null)?.replyId;
          const item: InboxItem = {
            id: replyId || `local-${Date.now()}`,
            itemKind: 'comment',
            platformCode: thread.platformCode,
            externalId: replyId || `local_${Date.now()}`,
            authorName: 'You',
            content: text,
            isHidden: false,
            isRead: true,
            isOutgoing: true,
            receivedAt: new Date().toISOString(),
            commentLikes: 0,
            replyCount: 0,
            post: thread.post
          };
          this.upsertItem(item);
          this.replyText.set('');
          this.clearCommentReplyTarget();
          this.banner.set('');
        } else {
          this.banner.set(res.message || 'Reply failed');
        }
        this.sending.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.banner.set(err?.error?.message || 'Reply failed');
        this.sending.set(false);
      }
    });
  }

  private pushLocalReply(key: string, text: string): void {
    const bubble: ThreadBubble = {
      id: `local-${Date.now()}`,
      content: text,
      at: new Date().toISOString(),
      outgoing: true
    };
    this.localOutgoing.update(map => ({
      ...map,
      [key]: [...(map[key] || []), bubble]
    }));
    this.replyText.set('');
  }
}
