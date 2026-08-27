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

    this.connection.on('inboxItem', (item: Record<string, unknown>) => {
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

  /** SignalR may send camelCase or PascalCase depending on server JSON settings. */
  private normalize(raw: Record<string, unknown>): InboxItem {
    const postRaw = (raw['post'] ?? raw['Post']) as Record<string, unknown> | undefined;
    const post = postRaw
      ? {
          postId: String(postRaw['postId'] ?? postRaw['PostId'] ?? ''),
          pageName: String(postRaw['pageName'] ?? postRaw['PageName'] ?? ''),
          postText: String(postRaw['postText'] ?? postRaw['PostText'] ?? ''),
          postImageUrl: (postRaw['postImageUrl'] ?? postRaw['PostImageUrl']) as string | undefined,
          likesCount: Number(postRaw['likesCount'] ?? postRaw['LikesCount'] ?? 0),
          commentsCount: Number(postRaw['commentsCount'] ?? postRaw['CommentsCount'] ?? 0),
          sharesCount: Number(postRaw['sharesCount'] ?? postRaw['SharesCount'] ?? 0),
          postedAt: String(postRaw['postedAt'] ?? postRaw['PostedAt'] ?? new Date().toISOString())
        }
      : undefined;

    return {
      id: String(raw['id'] ?? raw['Id'] ?? ''),
      itemKind: String(raw['itemKind'] ?? raw['ItemKind'] ?? ''),
      platformCode: String(raw['platformCode'] ?? raw['PlatformCode'] ?? ''),
      externalId: String(raw['externalId'] ?? raw['ExternalId'] ?? ''),
      authorName: String(raw['authorName'] ?? raw['AuthorName'] ?? ''),
      authorId: (raw['authorId'] ?? raw['AuthorId']) as string | undefined,
      content: String(raw['content'] ?? raw['Content'] ?? ''),
      isHidden: Boolean(raw['isHidden'] ?? raw['IsHidden']),
      isRead: Boolean(raw['isRead'] ?? raw['IsRead']),
      isOutgoing: Boolean(raw['isOutgoing'] ?? raw['IsOutgoing']),
      conversationId: (raw['conversationId'] ?? raw['ConversationId']) as string | undefined,
      receivedAt: String(raw['receivedAt'] ?? raw['ReceivedAt'] ?? new Date().toISOString()),
      post,
      commentLikes: Number(raw['commentLikes'] ?? raw['CommentLikes'] ?? 0),
      replyCount: Number(raw['replyCount'] ?? raw['ReplyCount'] ?? 0),
      parentId: (raw['parentId'] ?? raw['ParentId']) as string | undefined,
      replyToId: (raw['replyToId'] ?? raw['ReplyToId']) as string | undefined,
      replyToAuthor: (raw['replyToAuthor'] ?? raw['ReplyToAuthor']) as string | undefined,
      replyToContent: (raw['replyToContent'] ?? raw['ReplyToContent']) as string | undefined,
      menuType: (raw['menuType'] ?? raw['MenuType']) as string | undefined,
      pageId: (raw['pageId'] ?? raw['PageId']) as string | undefined,
      accountId: (raw['accountId'] ?? raw['AccountId']) as string | undefined
    };
  }
}
