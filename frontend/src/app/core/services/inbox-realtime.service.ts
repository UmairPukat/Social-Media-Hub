import { Injectable, inject, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { InboxItem } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class InboxRealtimeService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;
  private readonly itemSubject = new Subject<InboxItem>();

  readonly item$ = this.itemSubject.asObservable();

  async start(): Promise<void> {
    const token = this.auth.getToken();
    if (!token) return;
    if (this.connection?.state === HubConnectionState.Connected) return;

    const hubUrl = environment.hubUrl || environment.apiUrl.replace(/\/api\/?$/, '') + '/hubs/inbox';
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.getToken() || '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('inboxItem', (item: InboxItem) => {
      this.itemSubject.next(this.normalize(item));
    });

    await this.connection.start();
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    await this.connection.stop();
    this.connection = null;
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private normalize(item: InboxItem): InboxItem {
    return {
      ...item,
      id: String(item.id),
      conversationId: item.conversationId ? String(item.conversationId) : undefined,
      receivedAt: item.receivedAt || new Date().toISOString()
    };
  }
}
