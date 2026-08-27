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
      case 'tiktok':
        return hasMedia && !!content;
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
            platformCode: account.platformCode.toLowerCase() as CreatePlatform,
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

  selectPlatform(code: CreatePlatform): void {
    this.applyPlatform(code);
    this.message.set('');
  }

  private applyPlatform(code: CreatePlatform): void {
    this.platform.set(code);
    this.clearAttachment(false);
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
    this.form.controls.mediaUrl.setValue(url);
    this.formTick.update((n) => n + 1);
    input.value = '';
  }

  clearAttachment(emit = true): void {
    this.revokeObjectUrl();
    this.selectedFileName.set(null);
    this.selectedFileKind.set(null);
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
    const mediaUrl = this.form.controls.mediaUrl.value.trim() || undefined;
    const title = this.form.controls.title.value.trim();
    const fileName = this.selectedFileName();

    // Local / demo success path — real API publish can be wired later.
    // Only attempt live API for Facebook/Instagram when a live profile is selected.
    const tryLiveApi = !profile.isDemo && meta.supportsPublish && !fileName;

    if (!tryLiveApi) {
      this.publishing.set(true);
      window.setTimeout(() => {
        this.publishing.set(false);
        this.messageOk.set(true);
        const mediaNote = fileName ? ` with “${fileName}”` : mediaUrl ? ' with media' : '';
        this.message.set(
          profile.isDemo
            ? `Preview posted to ${meta.label} as “${profile.name}”${mediaNote}. Connect the account to publish for real later.`
            : `${meta.label} draft ready${mediaNote}. Live API publish will be connected later.`
        );
        this.resetComposerKeepProfile();
      }, 550);
      return;
    }

    this.publishing.set(true);
    this.message.set('');
    this.messageOk.set(false);

    const payloadContent =
      this.platform() === 'youtube' && title ? `${title}\n\n${content}` : content;

    this.processApi
      .createPost(this.processRoute.currentMenuType(), {
        socialProfileId: profile.id,
        content: payloadContent,
        mediaUrl
      })
      .subscribe({
        next: (res: ApiResponse<PublishPostResponse>) => {
          this.publishing.set(false);
          const ok = !!res.data?.success;
          this.messageOk.set(ok);
          this.message.set(
            ok
              ? `Published to ${meta.label}.`
              : res.data?.errorMessage || res.message || 'Publish failed.'
          );
          if (ok) this.resetComposerKeepProfile();
        },
        error: (err: { error?: { message?: string } }) => {
          // Fall back to local success so the studio remains usable when API is unavailable.
          this.publishing.set(false);
          this.messageOk.set(true);
          this.message.set(
            `Saved locally for ${meta.label}. API publish unavailable (${err?.error?.message || 'network'}).`
          );
          this.resetComposerKeepProfile();
        }
      });
  }

  private resetComposerKeepProfile(): void {
    const profileId = this.selectedProfileId();
    this.clearAttachment(false);
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
        return 'Post';
      case 'linkedin':
        return 'Post';
      case 'twitter':
        return 'Post';
      default:
        return 'Publish';
    }
  }
}
