import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { ApiService } from '../../core/services/api.service';
import { SocialPlatform, SocialPost } from '../../core/models/models';

@Component({
  selector: 'app-posts',
  standalone: true,
  imports: [CommonModule, MatChipsModule],
  templateUrl: './posts.component.html',
  styleUrl: './posts.component.scss'
})
export class PostsComponent implements OnInit {
  readonly posts = signal<SocialPost[]>([]);
  readonly SocialPlatform = SocialPlatform;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getPosts().subscribe({
      next: (res) => this.posts.set(res.data || [])
    });
  }

  platformName(platform: SocialPlatform): string {
    return SocialPlatform[platform] || 'Unknown';
  }

  statusLabel(status: number): string {
    return ['Draft', 'Scheduled', 'Published', 'Failed'][status] || 'Unknown';
  }
}
