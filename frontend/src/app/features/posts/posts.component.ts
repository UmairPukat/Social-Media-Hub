import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import {
  ApiResponse,
  PLATFORM_COLORS,
  PlatformCard,
  SocialPost,
  YouTubePostStatistics
} from '../../core/models/api.models';
import { CREATE_PLATFORMS } from '../../core/data/create-post.data';

type PostStatusFilter = 'all' | 0 | 1 | 2 | 3;

interface PostStatusMeta {
  label: string;
  icon: string;
  tone: string;
}

const POST_STATUSES: Record<number, PostStatusMeta> = {
  0: { label: 'Draft', icon: 'edit_note', tone: 'draft' },
  1: { label: 'Published', icon: 'check_circle', tone: 'published' },
  2: { label: 'Failed', icon: 'error', tone: 'failed' },
  3: { label: 'Scheduled', icon: 'schedule', tone: 'scheduled' },
  4: { label: 'Deleted', icon: 'delete', tone: 'deleted' }
};

const PLATFORM_ICONS: Record<string, string> = {
  facebook: 'public',
  instagram: 'photo_camera',
  threads: 'tag',
  twitter: 'alternate_email',
  x: 'alternate_email',
  linkedin: 'work',
  tiktok: 'music_note',
  youtube: 'smart_display',
  pinterest: 'push_pin',
  reddit: 'forum',
  snapchat: 'photo_camera_front',
  whatsapp: 'chat',
  telegram: 'send',
  discord: 'sports_esports'
};

const FALLBACK_COLORS = ['#2563eb', '#7c3aed', '#0891b2', '#0f766e', '#ea580c', '#db2777'];

@Component({
  selector: 'app-posts',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './posts.component.html',
  styleUrl: './posts.component.scss'
})
export class PostsComponent implements OnInit {
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);

  readonly posts = signal<SocialPost[]>([]);
  readonly platforms = signal<PlatformCard[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly notice = signal('');
  readonly search = signal('');
  readonly selectedPlatform = signal('all');
  readonly selectedStatus = signal<PostStatusFilter>('all');
  readonly pendingDelete = signal<string | null>(null);
  readonly deleting = signal<string | null>(null);
  readonly statsPostId = signal<string | null>(null);
  readonly statsPlatformCode = signal('');
  readonly statsLoading = signal(false);
  readonly statsError = signal('');
  readonly statsData = signal<YouTubePostStatistics | null>(null);

  private readonly statsDialog = viewChild<ElementRef<HTMLDialogElement>>('statsDialog');

  readonly statusFilters: { value: PostStatusFilter; label: string; icon: string }[] = [
    { value: 'all', label: 'All posts', icon: 'view_stream' },
    { value: 1, label: 'Published', icon: 'check_circle' },
    { value: 3, label: 'Scheduled', icon: 'schedule' },
    { value: 0, label: 'Drafts', icon: 'edit_note' },
    { value: 2, label: 'Needs attention', icon: 'error' }
  ];

  readonly postPlatforms = computed(() => {
    const composerCodes = new Set<string>(CREATE_PLATFORMS.map((item) => item.code));
    return this.platforms()
      .filter((item) => {
        const code = item.code.toLowerCase();
        return item.supportsPosts || item.category.toLowerCase() === 'social' || composerCodes.has(code);
      })
      .sort((a, b) => a.sortOrder - b.sortOrder);
  });

  readonly visiblePosts = computed(() => {
    const platform = this.selectedPlatform();
    const status = this.selectedStatus();
    const query = this.search().trim().toLowerCase();

    return this.posts().filter((post) => {
      const card = this.platformFor(post);
      const matchesPlatform =
        platform === 'all' ||
        post.platformId === platform ||
        card?.platformId === platform;
      const matchesStatus = status === 'all' || post.status === status;
      const haystack = [
        post.text,
        post.caption,
        post.profileName,
        post.profileUsername,
        post.platformCode,
        card?.displayName
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return matchesPlatform && matchesStatus && (!query || haystack.includes(query));
    });
  });

  readonly totalCount = computed(() => this.posts().length);
  readonly publishedCount = computed(() => this.posts().filter((post) => post.status === 1).length);
  readonly scheduledCount = computed(() => this.posts().filter((post) => post.status === 3).length);
  readonly attentionCount = computed(() => this.posts().filter((post) => post.status === 2).length);
  readonly demoMode = computed(() => this.posts().some((post) => post.id.startsWith('demo-')));
  readonly createPostLink = computed(() => `${this.processRoute.currentRouteBase()}/create-post`);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.loadError.set('');
    forkJoin({
      posts: this.processApi.getPosts(this.processRoute.currentMenuType()),
      platforms: this.processApi.getPlatforms(this.processRoute.currentMenuType())
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result: {
          posts: ApiResponse<SocialPost[]>;
          platforms: ApiResponse<PlatformCard[]>;
        }) => {
          const platforms = result.platforms.data || [];
          const posts = result.posts.data || [];
          this.platforms.set(platforms);
          this.posts.set(posts.length ? posts : this.buildDemoPosts(platforms));
        },
        error: () => this.loadError.set('We could not load your content library. Check the API connection and try again.')
      });
  }

  setPlatform(id: string): void {
    this.selectedPlatform.set(id);
  }

  setStatus(status: PostStatusFilter): void {
    this.selectedStatus.set(status);
  }

  setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  clearFilters(): void {
    this.search.set('');
    this.selectedPlatform.set('all');
    this.selectedStatus.set('all');
  }

  statusMeta(status: number): PostStatusMeta {
    return POST_STATUSES[status] || { label: 'Unknown', icon: 'help', tone: 'draft' };
  }

  statusCount(status: PostStatusFilter): number {
    if (status === 'all') return this.totalCount();
    return this.posts().filter((post) => post.status === status).length;
  }

  platformFor(post: SocialPost): PlatformCard | undefined {
    const code = post.platformCode?.toLowerCase();
    return this.platforms().find(
      (item) => item.platformId === post.platformId || (!!code && item.code.toLowerCase() === code)
    );
  }

  platformCode(post: SocialPost): string {
    return (post.platformCode || this.platformFor(post)?.code || 'social').toLowerCase();
  }

  platformName(post: SocialPost): string {
    return this.platformFor(post)?.displayName || post.platformCode || 'Social post';
  }

  platformIcon(code: string): string {
    return PLATFORM_ICONS[code.toLowerCase()] || 'campaign';
  }

  platformColor(code: string): string {
    const key = code.toLowerCase();
    if (PLATFORM_COLORS[key]) return PLATFORM_COLORS[key];
    const sum = [...key].reduce((total, char) => total + char.charCodeAt(0), 0);
    return FALLBACK_COLORS[sum % FALLBACK_COLORS.length];
  }

  content(post: SocialPost): string {
    return (post.text || post.caption || '').trim();
  }

  isDemo(post: SocialPost): boolean {
    return post.id.startsWith('demo-');
  }

  supportsStatistics(post: SocialPost): boolean {
    const code = this.platformCode(post);
    return (
      (code === 'youtube' || code === 'tiktok') &&
      post.status === 1 &&
      !this.isDemo(post) &&
      !!post.externalPostId
    );
  }

  statsPlatformLabel(): string {
    return this.statsPlatformCode() === 'tiktok' ? 'TikTok statistics' : 'YouTube statistics';
  }

  statsPermalinkLabel(): string {
    return this.statsPlatformCode() === 'tiktok' ? 'Open on TikTok' : 'Open on YouTube';
  }

  openStatistics(post: SocialPost): void {
    if (!this.supportsStatistics(post)) return;

    this.statsPostId.set(post.id);
    this.statsPlatformCode.set(this.platformCode(post));
    this.statsLoading.set(true);
    this.statsError.set('');
    this.statsData.set(null);

    const dialog = this.statsDialog()?.nativeElement;
    if (dialog && !dialog.open) dialog.showModal();

    this.processApi
      .getPostStatistics(this.processRoute.currentMenuType(), post.id, true)
      .pipe(finalize(() => this.statsLoading.set(false)))
      .subscribe({
        next: (res) => {
          if (!res.success || !res.data) {
            this.statsError.set(res.message || 'Could not load statistics.');
            return;
          }
          this.statsData.set(res.data);
          this.posts.update((items) =>
            items.map((item) =>
              item.id === post.id
                ? {
                    ...item,
                    viewCount: res.data!.viewCount,
                    likeCount: res.data!.likeCount,
                    commentCount: res.data!.commentCount,
                    shareCount: res.data!.shareCount
                  }
                : item
            )
          );
        },
        error: (err: { error?: { message?: string } }) =>
          this.statsError.set(err?.error?.message || 'Could not load statistics.')
      });
  }

  closeStatistics(): void {
    const dialog = this.statsDialog()?.nativeElement;
    if (dialog?.open) {
      dialog.close();
      return;
    }
    this.resetStatistics();
  }

  onStatsClosed(): void {
    this.resetStatistics();
  }

  private resetStatistics(): void {
    this.statsPostId.set(null);
    this.statsPlatformCode.set('');
    this.statsLoading.set(false);
    this.statsError.set('');
    this.statsData.set(null);
  }

  statsPostTitle(): string {
    const stats = this.statsData();
    if (stats?.title) return stats.title;
    const post = this.posts().find((item) => item.id === this.statsPostId());
    return this.content(post || ({} as SocialPost)) || 'Video statistics';
  }

  initials(post: SocialPost): string {
    const name = post.profileName || this.platformName(post);
    return name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() || '')
      .join('');
  }

  requestDelete(id: string): void {
    this.pendingDelete.set(id);
    this.notice.set('');
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  remove(id: string): void {
    if (this.deleting()) return;
    if (id.startsWith('demo-')) {
      this.posts.update((items) => items.filter((post) => post.id !== id));
      this.pendingDelete.set(null);
      this.notice.set('Preview post removed from this session.');
      return;
    }

    this.deleting.set(id);
    this.processApi
      .deletePost(this.processRoute.currentMenuType(), id)
      .pipe(finalize(() => this.deleting.set(null)))
      .subscribe({
        next: () => {
          this.posts.update((items) => items.filter((post) => post.id !== id));
          this.pendingDelete.set(null);
          this.notice.set('Post removed from the content library.');
        },
        error: () => this.notice.set('The post could not be deleted. Please try again.')
      });
  }

  private buildDemoPosts(platforms: PlatformCard[]): SocialPost[] {
    const now = Date.now();
    const platformId = (code: string) =>
      platforms.find((item) => item.code.toLowerCase() === code)?.platformId || `demo-${code}`;
    const ago = (hours: number) => new Date(now - hours * 60 * 60 * 1000).toISOString();
    const ahead = (hours: number) => new Date(now + hours * 60 * 60 * 1000).toISOString();

    return [
      {
        id: 'demo-facebook-launch',
        socialProfileId: 'demo-profile-facebook',
        platformId: platformId('facebook'),
        platformCode: 'facebook',
        profileName: 'SocialHub',
        profileUsername: 'socialhub',
        externalPostId: 'preview-fb-001',
        text: 'One workspace. Every conversation. Every channel. 🚀\n\nSocialHub brings publishing, engagement, and customer conversations together so your team can move faster.',
        status: 1,
        likeCount: 1284,
        commentCount: 96,
        shareCount: 214,
        viewCount: 18420,
        publishedAt: ago(3),
        createdAt: ago(4)
      },
      {
        id: 'demo-instagram-story',
        socialProfileId: 'demo-profile-instagram',
        platformId: platformId('instagram'),
        platformCode: 'instagram',
        profileName: 'SocialHub Studio',
        profileUsername: 'socialhub.official',
        externalPostId: 'preview-ig-001',
        caption: 'Behind every great campaign is a clear workflow. Plan, publish, and learn from one beautifully organized content calendar. ✨\n\n#SocialMedia #ContentStrategy #SocialHub',
        status: 1,
        likeCount: 3421,
        commentCount: 187,
        shareCount: 403,
        viewCount: 42180,
        publishedAt: ago(19),
        createdAt: ago(20)
      },
      {
        id: 'demo-linkedin-scheduled',
        socialProfileId: 'demo-profile-linkedin',
        platformId: platformId('linkedin'),
        platformCode: 'linkedin',
        profileName: 'SocialHub Company',
        profileUsername: 'socialhub',
        text: 'Social teams do their best work when publishing and customer care share the same source of truth. Next week, we’re sharing our framework for building a connected social operation.',
        status: 3,
        likeCount: 0,
        commentCount: 0,
        shareCount: 0,
        viewCount: 0,
        publishedAt: ahead(25),
        createdAt: ago(2)
      },
      {
        id: 'demo-twitter-draft',
        socialProfileId: 'demo-profile-twitter',
        platformId: platformId('twitter'),
        platformCode: 'twitter',
        profileName: 'SocialHub',
        profileUsername: 'socialhub',
        text: 'Your social workflow should feel this simple:\n\nPlan → Create → Approve → Publish → Learn\n\nWhat would you add?',
        status: 0,
        likeCount: 0,
        commentCount: 0,
        shareCount: 0,
        viewCount: 0,
        createdAt: ago(1)
      },
      {
        id: 'demo-tiktok-performance',
        socialProfileId: 'demo-profile-tiktok',
        platformId: platformId('tiktok'),
        platformCode: 'tiktok',
        profileName: 'SocialHub Creators',
        profileUsername: 'socialhub',
        caption: 'Three reporting shortcuts every social media manager should know 👀 Save this for your next campaign review.',
        status: 1,
        likeCount: 12680,
        commentCount: 534,
        shareCount: 2187,
        viewCount: 186500,
        publishedAt: ago(31),
        createdAt: ago(32)
      },
      {
        id: 'demo-youtube-failed',
        socialProfileId: 'demo-profile-youtube',
        platformId: platformId('youtube'),
        platformCode: 'youtube',
        profileName: 'SocialHub Academy',
        profileUsername: 'SocialHubAcademy',
        text: 'How to build a cross-platform content calendar that your whole team will actually use.',
        status: 2,
        likeCount: 0,
        commentCount: 0,
        shareCount: 0,
        viewCount: 0,
        errorMessage: 'Video processing timed out. Check the source file and retry publishing.',
        createdAt: ago(7)
      },
      {
        id: 'demo-whatsapp-scheduled',
        socialProfileId: 'demo-profile-whatsapp',
        platformId: platformId('whatsapp'),
        platformCode: 'whatsapp',
        profileName: 'SocialHub Updates',
        profileUsername: 'Business account',
        text: 'New this week: faster approvals, improved post previews, and a refreshed content library. Tap below to see what changed.',
        status: 3,
        likeCount: 0,
        commentCount: 0,
        shareCount: 0,
        viewCount: 0,
        publishedAt: ahead(48),
        createdAt: ago(5)
      }
    ];
  }
}
