import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/services/api.service';
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
export class CreatePostComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);

  readonly platforms = CREATE_PLATFORMS;
  readonly platform = signal<CreatePlatform>('facebook');
  readonly profiles = signal<ComposerProfile[]>([]);
  readonly message = signal('');
  readonly messageOk = signal(false);
  readonly publishing = signal(false);
  readonly audience = signal<'Public' | 'Friends' | 'Only me'>('Public');
  readonly waAudience = signal<'Status' | 'Broadcast'>('Status');
  readonly ytVisibility = signal<'Public' | 'Unlisted' | 'Private'>('Public');
  readonly liAudience = signal<'Anyone' | 'Connections'>('Anyone');

  readonly form = this.fb.nonNullable.group({
    profileId: ['', Validators.required],
    content: ['', [Validators.required, Validators.maxLength(5000)]],
    mediaUrl: [''],
    title: [''],
    location: [''],
    feeling: ['']
  });

  readonly activeMeta = computed(() =>
    this.platforms.find(p => p.code === this.platform())!
  );

  readonly platformProfiles = computed(() =>
    this.profiles().filter(p => p.platformCode === this.platform())
  );

  readonly selectedProfile = computed(() =>
    this.platformProfiles().find(p => p.id === this.form.controls.profileId.value) || null
  );

  readonly charLimit = computed(() => {
    switch (this.platform()) {
      case 'instagram': return 2200;
      case 'tiktok': return 2200;
      case 'whatsapp': return 1000;
      case 'youtube': return 5000;
      case 'linkedin': return 3000;
      case 'twitter': return 280;
      default: return 63206;
    }
  });

  readonly canPublish = computed(() => {
    const content = this.form.controls.content.value.trim();
    const media = this.form.controls.mediaUrl.value.trim();
    const title = this.form.controls.title.value.trim();
    const profile = this.selectedProfile();
    if (!profile) return false;

    switch (this.platform()) {
      case 'instagram':
      case 'tiktok':
        return !!media && !!content;
      case 'youtube':
        return !!title && (!!media || !!content);
      case 'whatsapp':
        return !!content;
      default:
        return !!content || !!media;
    }
  });

  ngOnInit(): void {
    this.api.getAccounts().subscribe({
      next: (res: ApiResponse<SocialAccount[]>) => {
        const live: ComposerProfile[] = (res.data || []).flatMap(account =>
          (account.profiles || []).map(p => ({
            id: p.id,
            platformCode: account.platformCode.toLowerCase() as CreatePlatform,
            name: p.name || account.displayName,
            username: p.username,
            profileType: p.profileType,
            isDemo: false
          }))
        );

        const livePlatforms = new Set(live.map(p => p.platformCode));
        const demos = DEMO_COMPOSER_PROFILES.filter(d => !livePlatforms.has(d.platformCode));
        this.profiles.set([...live, ...demos]);
        this.applyPlatform('facebook');
      },
      error: () => {
        this.profiles.set([...DEMO_COMPOSER_PROFILES]);
        this.applyPlatform('facebook');
      }
    });
  }

  selectPlatform(code: CreatePlatform): void {
    this.applyPlatform(code);
    this.message.set('');
  }

  private applyPlatform(code: CreatePlatform): void {
    this.platform.set(code);
    const list = this.profiles().filter(p => p.platformCode === code);
    const preferLive = list.find(p => !p.isDemo) || list[0];
    this.form.controls.profileId.setValue(preferLive?.id || '');
    this.form.controls.content.setValue('');
    this.form.controls.mediaUrl.setValue('');
    this.form.controls.title.setValue('');
    this.form.controls.location.setValue('');
    this.form.controls.feeling.setValue('');
  }

  selectProfile(id: string): void {
    this.form.controls.profileId.setValue(id);
  }

  setAudience(value: 'Public' | 'Friends' | 'Only me'): void {
    this.audience.set(value);
  }

  initials(name?: string): string {
    const parts = (name || '?').trim().split(/\s+/).slice(0, 2);
    return parts.map(p => p[0]?.toUpperCase() || '').join('') || '?';
  }

  remaining(): number {
    return this.charLimit() - this.form.controls.content.value.length;
  }

  publish(): void {
    if (!this.canPublish() || this.publishing()) return;

    const profile = this.selectedProfile();
    if (!profile) return;

    const meta = this.activeMeta();
    const content = this.form.controls.content.value.trim();
    const mediaUrl = this.form.controls.mediaUrl.value.trim() || undefined;
    const title = this.form.controls.title.value.trim();

    // Demo / unsupported platforms: simulate native success without API publish.
    if (profile.isDemo || !meta.supportsPublish) {
      this.publishing.set(true);
      window.setTimeout(() => {
        this.publishing.set(false);
        this.messageOk.set(true);
        this.message.set(
          profile.isDemo
            ? `Preview posted to ${meta.label} as “${profile.name}” (demo profile — connect the account to publish for real).`
            : `${meta.label} publishing API is coming soon. Draft kept locally.`
        );
        this.form.controls.content.reset('');
        if (this.platform() === 'youtube') this.form.controls.title.reset('');
      }, 700);
      return;
    }

    this.publishing.set(true);
    this.message.set('');
    this.messageOk.set(false);

    const payloadContent =
      this.platform() === 'youtube' && title
        ? `${title}\n\n${content}`
        : content;

    this.api.createPost({
      socialProfileId: profile.id,
      content: payloadContent,
      mediaUrl
    }).subscribe({
      next: (res: ApiResponse<PublishPostResponse>) => {
        this.publishing.set(false);
        const ok = !!res.data?.success;
        this.messageOk.set(ok);
        this.message.set(
          ok
            ? `Published to ${meta.label}.`
            : res.data?.errorMessage || res.message || 'Publish failed.'
        );
        if (ok) {
          this.form.controls.content.reset('');
          this.form.controls.mediaUrl.reset('');
        }
      },
      error: (err: { error?: { message?: string } }) => {
        this.publishing.set(false);
        this.messageOk.set(false);
        this.message.set(err?.error?.message || 'Publish failed');
      }
    });
  }

  ctaLabel(): string {
    switch (this.platform()) {
      case 'facebook': return 'Post';
      case 'instagram': return 'Share';
      case 'whatsapp': return this.waAudience() === 'Status' ? 'Update status' : 'Send broadcast';
      case 'youtube': return 'Upload';
      case 'tiktok': return 'Post';
      case 'linkedin': return 'Post';
      case 'twitter': return 'Post';
      default: return 'Publish';
    }
  }
}
