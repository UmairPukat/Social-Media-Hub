import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { ProcessApiService } from '../../core/services/process-api.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import {
  CREATE_PLATFORMS,
  CreatePlatform,
  ComposerProfile,
  DEMO_COMPOSER_PROFILES
} from '../../core/data/create-post.data';
import { ApiResponse, PublishPostResponse, SocialAccount } from '../../core/models/api.models';

@Component({
  selector: 'app-create-post',
  standalone: true,
  imports: [DecimalPipe, ReactiveFormsModule, MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './create-post.component.html',
  styleUrl: './create-post.component.scss'
})
export class CreatePostComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly processApi = inject(ProcessApiService);
  private readonly processRoute = inject(ProcessRouteService);
  private formSub?: Subscription;
  private objectUrl: string | null = null;
  private selectedFile: File | null = null;

  readonly platforms = CREATE_PLATFORMS;
  readonly platform = signal<CreatePlatform>('facebook');
  readonly profiles = signal<ComposerProfile[]>([]);
  readonly selectedProfileId = signal('');
  /** Bumps whenever reactive form values change so computed publish rules stay fresh. */
  readonly formTick = signal(0);
  readonly message = signal('');
  readonly messageOk = signal(false);
  readonly publishing = signal(false);
  readonly selectedFileName = signal<string | null>(null);
  readonly selectedFileKind = signal<'image' | 'video' | 'file' | null>(null);
  readonly audience = signal<'Public' | 'Friends' | 'Only me'>('Public');
  readonly waAudience = signal<'Status' | 'Broadcast'>('Status');
  readonly ytVisibility = signal<'Public' | 'Unlisted' | 'Private'>('Public');
  readonly liAudience = signal<'Anyone' | 'Connections'>('Anyone');
  readonly ttPrivacy = signal<'Public' | 'Friends' | 'Followers' | 'Only you'>('Public');
  readonly ttAllowComment = signal(true);
  readonly ttAllowDuet = signal(true);
  readonly ttAllowStitch = signal(true);
  readonly ttDiscloseContent = signal(false);
  readonly ttYourBrand = signal(false);
  readonly ttBrandedContent = signal(false);
  readonly ttAutoAddMusic = signal(true);
  readonly ttAccountOpen = signal(false);
  readonly selectedFileSize = signal<number | null>(null);
  readonly videoDimensions = signal<{ width: number; height: number } | null>(null);

  readonly form = this.fb.nonNullable.group({
    profileId: ['', Validators.required],
    content: ['', [Validators.maxLength(5000)]],
    mediaUrl: [''],
    title: [''],
    location: [''],
    feeling: ['']
  });

  readonly activeMeta = computed(() =>
    this.platforms.find((p) => p.code === this.platform())!
  );

  readonly platformProfiles = computed(() =>
    this.profiles().filter((p) => p.platformCode === this.platform())
  );

  readonly selectedProfile = computed(() => {
    const id = this.selectedProfileId();
    return this.platformProfiles().find((p) => p.id === id) || null;
  });

  readonly charLimit = computed(() => {
    switch (this.platform()) {
      case 'instagram':
        return 2200;
      case 'tiktok':
        return 2200;
      case 'whatsapp':
        return 1000;
      case 'youtube':
        return 5000;
      case 'linkedin':
        return 3000;
      case 'twitter':
        return 280;
      default:
        return 63206;
    }
  });

  readonly canPublish = computed(() => {
    this.formTick();
    const meta = this.activeMeta();
    const profile = this.selectedProfile();
    if (!profile) return false;

    const content = this.form.controls.content.value.trim();
    const media = this.form.controls.mediaUrl.value.trim();
    const title = this.form.controls.title.value.trim();
    const hasFile = !!this.selectedFileName();
    const hasMedia = !!media || hasFile;

    switch (this.platform()) {
      case 'instagram':
        return hasMedia && !!content;
      case 'tiktok': {
        const hasVideo =
          this.selectedFileKind() === 'video' ||
          (!!media && /\.(mp4|mov|webm)(\?|$)/i.test(media));
        const hasImage =
          this.selectedFileKind() === 'image' ||
          (!!media && /\.(jpg|jpeg|png|webp|gif)(\?|$)/i.test(media));
        return (hasVideo || hasImage) && !!content;
      }
      case 'youtube':
        return !!title && hasMedia;
      case 'whatsapp':
      case 'facebook':
      case 'linkedin':
      case 'twitter':
        return !!content || hasMedia;
      default:
        return meta.requiresMedia ? hasMedia && (!!content || !!title) : !!content || hasMedia;
    }
  });

  ngOnInit(): void {
    this.formSub = this.form.valueChanges.subscribe(() => {
      this.formTick.update((n) => n + 1);
    });

    this.processApi.getAccounts(this.processRoute.currentMenuType()).subscribe({
      next: (res: ApiResponse<SocialAccount[]>) => {
        const live: ComposerProfile[] = (res.data || []).flatMap((account) =>
          (account.profiles || []).map((p) => ({
            id: p.id,
            platformCode: this.toComposerPlatform(account.platformCode),
            name: p.name || account.displayName,
            username: p.username,
            profileType: p.profileType,
            isDemo: false
          }))
        );

        const livePlatforms = new Set(live.map((p) => p.platformCode));
        const demos = DEMO_COMPOSER_PROFILES.filter((d) => !livePlatforms.has(d.platformCode));
        this.profiles.set([...live, ...demos]);
        this.applyPlatform('facebook');
      },
      error: () => {
        this.profiles.set([...DEMO_COMPOSER_PROFILES]);
        this.applyPlatform('facebook');
      }
    });
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.revokeObjectUrl();
  }

  private toComposerPlatform(code: string): CreatePlatform {
    const normalized = (code || '').toLowerCase();
    if (normalized === 'instagram_login') return 'instagram';
    return normalized as CreatePlatform;
  }

  selectPlatform(code: CreatePlatform): void {
    this.applyPlatform(code);
    this.message.set('');
  }

  private applyPlatform(code: CreatePlatform): void {
    this.platform.set(code);
    this.clearAttachment(false);
    this.resetTikTokSettings();
    const list = this.profiles().filter((p) => p.platformCode === code);
    const preferLive = list.find((p) => !p.isDemo) || list[0];
    const id = preferLive?.id || '';
    this.selectedProfileId.set(id);
    this.form.patchValue(
      {
        profileId: id,
        content: '',
        mediaUrl: '',
        title: '',
        location: '',
        feeling: ''
      },
      { emitEvent: true }
    );
  }

  selectProfile(id: string): void {
    this.selectedProfileId.set(id);
    this.form.controls.profileId.setValue(id);
    this.ttAccountOpen.set(false);
  }

  toggleTtAccountMenu(): void {
    this.ttAccountOpen.update((open) => !open);
  }

  closeTtAccountMenu(): void {
    this.ttAccountOpen.set(false);
  }

  toggleTtDisclose(): void {
    const next = !this.ttDiscloseContent();
    this.ttDiscloseContent.set(next);
    if (!next) {
      this.ttYourBrand.set(false);
      this.ttBrandedContent.set(false);
    this.ttAutoAddMusic.set(true);
    }
  }

  private resetTikTokSettings(): void {
    this.ttPrivacy.set('Public');
    this.ttAllowComment.set(true);
    this.ttAllowDuet.set(true);
    this.ttAllowStitch.set(true);
    this.ttDiscloseContent.set(false);
    this.ttYourBrand.set(false);
    this.ttBrandedContent.set(false);
    this.ttAutoAddMusic.set(true);
    this.ttAccountOpen.set(false);
  }

  tikTokCaptionCount(): number {
    return this.form.controls.content.value.length;
  }

  fileFormatLabel(): string {
    const name = this.selectedFileName();
    if (!name) return '—';
    const ext = name.split('.').pop()?.toUpperCase();
    return ext || '—';
  }

  fileSizeLabel(): string {
    const bytes = this.selectedFileSize();
    if (bytes == null) return '—';
    if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)}MB`;
    if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${bytes}B`;
  }

  videoResolutionLabel(): string {
    const dims = this.videoDimensions();
    if (!dims) return '—';
    const height = dims.height;
    if (height >= 2160) return '4K';
    if (height >= 1440) return '1440P';
    if (height >= 1080) return '1080P';
    if (height >= 720) return '720P';
    return `${dims.width}×${dims.height}`;
  }

  setAudience(value: 'Public' | 'Friends' | 'Only me'): void {
    this.audience.set(value);
  }

  initials(name?: string): string {
    const parts = (name || '?').trim().split(/\s+/).slice(0, 2);
    return parts.map((p) => p[0]?.toUpperCase() || '').join('') || '?';
  }

  remaining(): number {
    return this.charLimit() - this.form.controls.content.value.length;
  }

  isImagePreview(): boolean {
    const kind = this.selectedFileKind();
    if (kind === 'image') return true;
    if (kind === 'video' || kind === 'file') return false;
    const url = this.form.controls.mediaUrl.value.trim().toLowerCase();
    return !!url && !/\.(mp4|webm|mov|mkv)(\?|$)/i.test(url);
  }

  isVideoPreview(): boolean {
    const kind = this.selectedFileKind();
    if (kind === 'video') return true;
    const url = this.form.controls.mediaUrl.value.trim().toLowerCase();
    return !!url && /\.(mp4|webm|mov|mkv)(\?|$)/i.test(url);
  }

  onFilePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.revokeObjectUrl();
    const url = URL.createObjectURL(file);
    this.objectUrl = url;

    const kind = file.type.startsWith('image/')
      ? 'image'
      : file.type.startsWith('video/')
        ? 'video'
        : 'file';

    this.selectedFileName.set(file.name);
    this.selectedFileKind.set(kind);
    this.selectedFile = file;
    this.selectedFileSize.set(file.size);
    this.videoDimensions.set(null);
    this.form.controls.mediaUrl.setValue(url);
    this.formTick.update((n) => n + 1);
    if (kind === 'video') {
      this.loadVideoDimensions(url);
    }
    input.value = '';
  }

  private loadVideoDimensions(url: string): void {
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.onloadedmetadata = () => {
      this.videoDimensions.set({
        width: video.videoWidth,
        height: video.videoHeight
      });
      video.removeAttribute('src');
      video.load();
    };
    video.src = url;
  }

  clearAttachment(emit = true): void {
    this.revokeObjectUrl();
    this.selectedFile = null;
    this.selectedFileName.set(null);
    this.selectedFileKind.set(null);
    this.selectedFileSize.set(null);
    this.videoDimensions.set(null);
    if (emit) {
      this.form.controls.mediaUrl.setValue('');
      this.formTick.update((n) => n + 1);
    }
  }

  private revokeObjectUrl(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }

  publish(): void {
    if (!this.canPublish() || this.publishing()) return;

    const profile = this.selectedProfile();
    if (!profile) return;

    const meta = this.activeMeta();
    const content = this.form.controls.content.value.trim();
    const mediaUrl = this.form.controls.mediaUrl.value.trim();
    const title = this.form.controls.title.value.trim();
    const file = this.selectedFile;

    if (profile.isDemo || !meta.supportsPublish) {
      this.publishing.set(true);
      window.setTimeout(() => {
        this.publishing.set(false);
        this.messageOk.set(true);
        const mediaNote = file ? ` with “${file.name}”` : mediaUrl ? ' with media' : '';
        this.message.set(
          profile.isDemo
            ? `Preview posted to ${meta.label} as “${profile.name}”${mediaNote}. Connect the account to publish for real.`
            : `${meta.label} publishing is not available yet${mediaNote}.`
        );
        this.resetComposerKeepProfile();
      }, 550);
      return;
    }

    this.publishing.set(true);
    this.message.set('');
    this.messageOk.set(false);

    const formData = new FormData();
    formData.append('socialProfileId', profile.id);
    formData.append('content', content);
    if (title) formData.append('title', title);
    if (this.platform() === 'youtube') {
      formData.append('visibility', this.ytVisibility().toLowerCase());
    }
    if (this.platform() === 'tiktok') {
      formData.append('privacy', this.ttPrivacy().toLowerCase().replace(' ', '_'));
      formData.append('allowComment', String(this.ttAllowComment()));
      formData.append('allowDuet', String(this.ttAllowDuet()));
      formData.append('allowStitch', String(this.ttAllowStitch()));
      formData.append('discloseContent', String(this.ttDiscloseContent()));
      formData.append('yourBrand', String(this.ttYourBrand()));
      formData.append('brandedContent', String(this.ttBrandedContent()));
      formData.append('autoAddMusic', String(this.ttAutoAddMusic()));
    }
    if (file) {
      formData.append('mediaFile', file, file.name);
    } else if (mediaUrl && !mediaUrl.startsWith('blob:')) {
      formData.append('mediaUrl', mediaUrl);
    }

    this.processApi.createPost(this.processRoute.currentMenuType(), formData).subscribe({
      next: (res: ApiResponse<PublishPostResponse>) => {
        this.publishing.set(false);
        const ok = !!res.success && !!res.data?.success;
        this.messageOk.set(ok);
        this.message.set(
          ok
            ? `Published to ${meta.label} as “${profile.name}”.`
            : res.data?.errorMessage || res.message || 'Publish failed.'
        );
        if (ok) this.resetComposerKeepProfile();
      },
      error: (err: { error?: { message?: string } }) => {
        this.publishing.set(false);
        this.messageOk.set(false);
        this.message.set(err?.error?.message || 'Publish failed. Check the connection and try again.');
      }
    });
  }

  private resetComposerKeepProfile(): void {
    const profileId = this.selectedProfileId();
    this.clearAttachment(false);
    this.resetTikTokSettings();
    this.form.patchValue(
      {
        profileId,
        content: '',
        mediaUrl: '',
        title: '',
        location: '',
        feeling: ''
      },
      { emitEvent: true }
    );
  }

  readonly publishStatusTitle = computed(() => {
    if (!this.publishing()) return '';
    switch (this.platform()) {
      case 'instagram':
        return 'Publishing to Instagram';
      case 'youtube':
        return 'Uploading to YouTube';
      case 'facebook':
        return 'Posting to Facebook';
      case 'tiktok':
        return 'Uploading to TikTok';
      default:
        return 'Publishing';
    }
  });

  readonly publishStatusHint = computed(() => {
    if (!this.publishing()) return '';
    switch (this.platform()) {
      case 'instagram':
        return 'Creating your media and waiting for Instagram to finish processing…';
      case 'youtube':
        return 'Uploading your video. This can take a minute or two.';
      case 'facebook':
        return 'Sending your post to Facebook…';
      case 'tiktok':
        return 'Preparing your video and upload settings…';
      default:
        return 'Please wait while we publish your content.';
    }
  });

  ctaLabel(): string {
    switch (this.platform()) {
      case 'facebook':
        return 'Post';
      case 'instagram':
        return 'Share';
      case 'whatsapp':
        return this.waAudience() === 'Status' ? 'Update status' : 'Send broadcast';
      case 'youtube':
        return 'Upload';
      case 'tiktok':
        return 'Upload';
      case 'linkedin':
        return 'Post';
      case 'twitter':
        return 'Post';
      default:
        return 'Publish';
    }
  }
}
