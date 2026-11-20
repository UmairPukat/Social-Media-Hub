import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MetaAdsApiService } from '../../core/services/meta-ads-api.service';
import { MetaAdsStateService } from '../../core/services/meta-ads-state.service';
import { ProcessRouteService } from '../../core/services/process-route.service';
import { MetaLead, MetaLeadForm, MetaPageOption } from '../../core/models/meta-ads.models';
import { watchMetaAdsMenu } from './meta-ads-menu.watch';
import { metaAdsModuleLabel, metaErrorMessage } from './meta-ads.util';

@Component({
  selector: 'app-meta-ads-facebook-leads',
  standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  templateUrl: './meta-ads-facebook-leads.component.html',
  styleUrl: './meta-ads.shared.scss'
})
export class MetaAdsFacebookLeadsComponent implements OnInit {
  private readonly api = inject(MetaAdsApiService);
  readonly state = inject(MetaAdsStateService);
  private readonly processRoute = inject(ProcessRouteService);
  readonly moduleLabel = () => metaAdsModuleLabel(this.processRoute.currentMenuType());
  readonly statuses = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];

  readonly pages = signal<MetaPageOption[]>([]);
  readonly forms = signal<MetaLeadForm[]>([]);
  readonly leads = signal<MetaLead[]>([]);
  readonly selectedPageId = signal('');
  readonly selectedFormId = signal('');
  readonly formsCursor = signal<string | null>(null);
  readonly leadsCursor = signal<string | null>(null);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly selectedLead = signal<MetaLead | null>(null);
  readonly notesDraft = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly banner = signal('');

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    const from = this.dateFrom();
    const to = this.dateTo();
    return this.leads().filter(lead => {
      if (status && lead.status !== status) return false;
      const day = (lead.createdTime ?? '').slice(0, 10);
      if (from && day && day < from) return false;
      if (to && day && day > to) return false;
      if (!term) return true;
      const haystack = [lead.id, lead.customerName, lead.email, lead.phone, lead.formName, lead.campaignName]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return haystack.includes(term);
    });
  });

  constructor() {
    watchMetaAdsMenu(() => this.reload());
  }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.state.syncForCurrentProcess();
    this.pages.set([]);
    this.forms.set([]);
    this.leads.set([]);
    this.selectedLead.set(null);
    this.error.set('');
    this.banner.set('');
    this.loading.set(true);
    this.api.getPages(this.processRoute.currentMenuType()).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        const pages = res.data ?? [];
        this.pages.set(pages);
        const saved = this.state.selectedPage()?.id;
        const match = pages.find(page => page.id === saved) ?? pages[0];
        if (match) this.onPageChange(match.id);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load Facebook pages.');
      }
    });
  }

  onPageChange(pageId: string): void {
    this.selectedPageId.set(pageId);
    this.forms.set([]);
    this.leads.set([]);
    this.selectedFormId.set('');
    this.selectedLead.set(null);
    this.formsCursor.set(null);
    const page = this.pages().find(item => item.id === pageId);
    if (page) this.state.selectPage({ id: page.id, name: page.name });
    if (!pageId) return;
    this.subscribe(pageId);
    this.loadForms();
  }

  subscribe(pageId: string): void {
    this.api.subscribeLeadgen(this.processRoute.currentMenuType(), pageId).subscribe({
      next: (res) => {
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.banner.set('This page is subscribed to leadgen webhooks.');
      },
      error: () => this.error.set('Unable to subscribe this page to leadgen.')
    });
  }

  loadForms(after?: string): void {
    const pageId = this.selectedPageId();
    if (!pageId) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getLeadForms(this.processRoute.currentMenuType(), pageId, after).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        const items = res.data?.items ?? [];
        this.forms.set(after ? [...this.forms(), ...items] : items);
        this.formsCursor.set(res.data?.nextCursor ?? null);
        if (!after && items.length && !this.selectedFormId()) {
          this.onFormChange(items[0].id);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load lead forms.');
      }
    });
  }

  onFormChange(formId: string): void {
    this.selectedFormId.set(formId);
    this.leads.set([]);
    this.leadsCursor.set(null);
    this.selectedLead.set(null);
    if (formId) this.loadLeads();
  }

  loadLeads(after?: string): void {
    const form = this.forms().find(item => item.id === this.selectedFormId());
    const pageId = this.selectedPageId();
    if (!form || !pageId) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getLeads(this.processRoute.currentMenuType(), form.id, pageId, form.name, after).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        const items = res.data?.items ?? [];
        this.leads.set(after ? [...this.leads(), ...items] : items);
        this.leadsCursor.set(res.data?.nextCursor ?? null);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to retrieve leads.');
      }
    });
  }

  openLead(lead: MetaLead): void {
    this.selectedLead.set(lead);
    this.notesDraft.set(lead.notes ?? '');
    this.api.getLead(this.processRoute.currentMenuType(), lead.id, lead.pageId).subscribe({
      next: (res) => {
        if (!res.success || !res.data) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.selectedLead.set(res.data);
        this.notesDraft.set(res.data.notes ?? '');
      }
    });
  }

  saveLead(status?: string): void {
    const lead = this.selectedLead();
    if (!lead) return;
    this.api.updateLead(this.processRoute.currentMenuType(), lead.id, {
      status: status ?? lead.status,
      notes: this.notesDraft()
    }).subscribe({
      next: (res) => {
        if (!res.success || !res.data) {
          this.error.set(metaErrorMessage(res));
          return;
        }
        this.selectedLead.set(res.data);
        this.leads.set(this.leads().map(item => item.id === res.data.id ? { ...item, ...res.data } : item));
        this.banner.set(`Lead ${res.data.id} is ${res.data.status}.`);
      },
      error: () => this.error.set('Unable to update the lead.')
    });
  }

  exportCsv(): void {
    const rows = this.filtered();
    const header = ['Lead ID', 'Customer Name', 'Email', 'Phone Number', 'Form Name', 'Campaign Name', 'Submission Date', 'Status'];
    const lines = [
      header.join(','),
      ...rows.map(lead => [
        lead.id,
        lead.customerName,
        lead.email,
        lead.phone,
        lead.formName,
        lead.campaignName,
        lead.createdTime,
        lead.status
      ].map(value => `"${(value ?? '').replace(/"/g, '""')}"`).join(','))
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `facebook-leads-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
