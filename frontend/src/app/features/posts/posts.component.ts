import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../core/services/api.service';
import { SocialPost } from '../../core/models/api.models';

@Component({
  selector: 'app-posts',
  standalone: true,
  imports: [DatePipe, MatButtonModule],
  template: `
    <section class="page">
      <header>
        <h1>Posts</h1>
        <p>Posts stored under SocialProfile.</p>
      </header>
      <div class="list">
        @for (post of posts(); track post.id) {
          <article>
            <div class="top">
              <strong>{{ post.platformCode || 'Post' }}</strong>
              <span>{{ statusLabel(post.status) }}</span>
              <small>{{ post.createdAt | date: 'medium' }}</small>
            </div>
            <p>{{ post.text || post.caption }}</p>
            @if (post.errorMessage) {
              <p class="err">{{ post.errorMessage }}</p>
            }
            <button mat-button type="button" (click)="remove(post.id)">Delete</button>
          </article>
        } @empty {
          <p class="empty">No posts yet.</p>
        }
      </div>
    </section>
  `,
  styles: [`
    h1 { font-family: "Space Grotesk", sans-serif; margin: 0 0 6px; }
    p { color: #64748b; }
    .list { display: flex; flex-direction: column; gap: 12px; margin-top: 16px; }
    article { background:#fff; border:1px solid rgba(15,23,42,.06); border-radius:14px; padding:14px; }
    .top { display:flex; gap:10px; align-items:center; }
    small { margin-left:auto; color:#94a3b8; }
    .err { color:#b91c1c; }
  `]
})
export class PostsComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly posts = signal<SocialPost[]>([]);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.getPosts().subscribe(res => this.posts.set(res.data || []));
  }

  statusLabel(status: number): string {
    return ['Draft', 'Published', 'Failed', 'Scheduled', 'Deleted'][status] || 'Unknown';
  }

  remove(id: string): void {
    this.api.deletePost(id).subscribe(() => this.reload());
  }
}
