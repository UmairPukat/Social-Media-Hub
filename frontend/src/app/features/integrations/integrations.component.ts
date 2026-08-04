import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/services/api.service';
import { MetaAuthUrlService, MetaPlatform } from '../../core/services/meta-auth-url.service';
import { ApiResponse, PlatformCard } from '../../core/models/api.models';

export interface IntegrationCategoryGroup {
  id: string;
  label: string;
  accent: string;
  icon: string;
  items: PlatformCard[];
}

const CATEGORY_META: Record<string, { accent: string; icon: string }> = {
  social: { accent: '#2563eb', icon: 'share' },
  communication: { accent: '#16a34a', icon: 'forum' },
  commerce: { accent: '#ea580c', icon: 'storefront' },
  crm: { accent: '#7c3aed', icon: 'groups' },
  calendar: { accent: '#0891b2', icon: 'calendar_month' },
  storage: { accent: '#475569', icon: 'cloud' },
  payment: { accent: '#0f766e', icon: 'payments' },
  ai: { accent: '#db2777', icon: 'auto_awesome' }
};

const CATEGORY_ORDER = [
  'social',
  'communication',
  'commerce',
  'crm',
  'calendar',
  'storage',
  'payment',
  'ai'
];

@Component({
  selector: 'app-integrations',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './integrations.component.html',
  styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly metaAuth = inject(MetaAuthUrlService);

  readonly cards = signal<PlatformCard[]>([]);
  readonly message = signal('');
  readonly connecting = signal<string | null>(null);
  readonly activeCategory = signal<string>('all');

  readonly connectedCount = computed(() => this.cards().filter((c) => c.isConnected).length);

  readonly categories = computed<IntegrationCategoryGroup[]>(() => {
    const map = new Map<string, IntegrationCategoryGroup>();
    for (const card of this.cards()) {
      const id = (card.category || 'other').toLowerCase();
      if (!map.has(id)) {
        const meta = CATEGORY_META[id] || { accent: '#64748b', icon: 'extension' };
        map.set(id, {
          id,
          label: card.categoryLabel || id,
          accent: meta.accent,
          icon: meta.icon,
          items: []
        });
      }
      map.get(id)!.items.push(card);
    }

    return CATEGORY_ORDER
      .map((id) => map.get(id))
      .filter((g): g is IntegrationCategoryGroup => !!g && g.items.length > 0)
      .concat([...map.values()].filter((g) => !CATEGORY_ORDER.includes(g.id)));
  });

  readonly visibleCategories = computed(() => {
    const active = this.activeCategory();
    if (active === 'all') return this.categories();
    return this.categories().filter((c) => c.id === active);
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.getPlatforms().subscribe({
      next: (res: ApiResponse<PlatformCard[]>) => this.cards.set(res.data || []),
      error: () => this.message.set('Failed to load integrations')
    });
  }

  setCategory(id: string): void {
    this.activeCategory.set(id);
  }

  async connect(card: PlatformCard): Promise<void> {
    const code = card.code.toLowerCase() as MetaPlatform;
    if (!card.canConnect || !['facebook', 'instagram', 'whatsapp'].includes(code)) {
      this.message.set(`${card.displayName} is coming soon.`);
      return;
    }

    if (!this.metaAuth.isConfigured(code)) {
      this.message.set(`Set a real Meta App Id for ${card.displayName} in environment.ts and appsettings.json.`);
      return;
    }

    this.connecting.set(code);
    this.message.set(`Opening Meta login for ${card.displayName}…`);

    try {
      const result = await this.metaAuth.openPopup(code);
      if (result.ok) {
        this.message.set(`${card.displayName} connected.`);
        this.reload();
      } else {
        this.message.set(result.message || 'Connection failed');
      }
    } catch (err) {
      this.message.set(err instanceof Error ? err.message : 'Connection failed');
    } finally {
      this.connecting.set(null);
    }
  }

  disconnect(card: PlatformCard): void {
    this.api.disconnect(card.code).subscribe({
      next: () => {
        this.message.set(`${card.displayName} disconnected.`);
        this.reload();
      },
      error: (err: { error?: { message?: string } }) =>
        this.message.set(err?.error?.message || 'Disconnect failed')
    });
  }

  platformIcon(code: string): string {
    const map: Record<string, string> = {
      facebook: 'public',
      instagram: 'photo_camera',
      threads: 'tag',
      twitter: 'alternate_email',
      x: 'alternate_email',
      linkedin: 'work',
      tiktok: 'music_note',
      youtube: 'play_circle',
      pinterest: 'push_pin',
      reddit: 'forum',
      snapchat: 'photo_camera_front',
      whatsapp: 'chat',
      outlook: 'mail',
      gmail: 'mail_outline',
      microsoft365: 'apps',
      exchange: 'inbox',
      telegram: 'send',
      discord: 'sports_esports',
      slack: 'tag',
      teams: 'groups',
      shopify: 'storefront',
      woocommerce: 'shopping_bag',
      tiktokshop: 'store',
      amazon: 'local_shipping',
      etsy: 'store',
      ebay: 'gavel',
      salesforce: 'cloud',
      hubspot: 'hub',
      zoho: 'business',
      dynamics365: 'apartment',
      pipedrive: 'trending_up',
      googlecalendar: 'calendar_month',
      outlookcalendar: 'event',
      applecalendar: 'event_available',
      onedrive: 'cloud_queue',
      googledrive: 'add_to_drive',
      dropbox: 'folder',
      sharepoint: 'folder_shared',
      stripe: 'credit_card',
      paypal: 'account_balance_wallet',
      square: 'payments',
      openai: 'auto_awesome',
      azureopenai: 'cloud',
      claude: 'psychology',
      gemini: 'auto_awesome'
    };
    return map[code.toLowerCase()] || 'extension';
  }
}
