import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  DashboardSummary,
  InboxItem,
  MetaPage,
  OAuthCallbackRequest,
  PlatformCard,
  PublishPostResponse,
  SocialAccount,
  SocialPost
} from '../models/api.models';
import { MetaPlatform } from './meta-auth-url.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<ApiResponse<DashboardSummary>> {
    return this.http.get<ApiResponse<DashboardSummary>>(`${this.base}/Dashboard/GetSummary`);
  }

  getPlatforms(): Observable<ApiResponse<PlatformCard[]>> {
    return this.http.get<ApiResponse<PlatformCard[]>>(`${this.base}/SocialAccounts/GetPlatformCards`);
  }

  getAccounts(): Observable<ApiResponse<SocialAccount[]>> {
    return this.http.get<ApiResponse<SocialAccount[]>>(`${this.base}/SocialAccounts/GetConnectedAccounts`);
  }

  oauthCallback(platform: MetaPlatform, body: OAuthCallbackRequest): Observable<ApiResponse<SocialAccount>> {
    const action =
      platform === 'facebook' ? 'FacebookCallback'
      : platform === 'instagram' ? 'InstagramCallback'
      : 'WhatsAppCallback';
    return this.http.post<ApiResponse<SocialAccount>>(`${this.base}/Integrations/${action}`, body);
  }

  getMetaPages(platformCode: string): Observable<ApiResponse<MetaPage[]>> {
    return this.http.get<ApiResponse<MetaPage[]>>(
      `${this.base}/Integrations/GetPages?platformCode=${encodeURIComponent(platformCode)}`
    );
  }

  selectMetaPage(platformCode: string, pageId: string): Observable<ApiResponse<SocialAccount>> {
    return this.http.post<ApiResponse<SocialAccount>>(`${this.base}/Integrations/SelectPage`, {
      platformCode,
      pageId
    });
  }

  disconnect(platformCode: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(
      `${this.base}/SocialAccounts/Disconnect?platformCode=${encodeURIComponent(platformCode)}`,
      {}
    );
  }

  getPosts(platformId?: string): Observable<ApiResponse<SocialPost[]>> {
    const q = platformId ? `?platformId=${platformId}` : '';
    return this.http.get<ApiResponse<SocialPost[]>>(`${this.base}/Posts/GetPosts${q}`);
  }

  createPost(body: { socialProfileId: string; content: string; mediaUrl?: string }): Observable<ApiResponse<PublishPostResponse>> {
    return this.http.post<ApiResponse<PublishPostResponse>>(`${this.base}/Posts/CreateAndPublish`, body);
  }

  deletePost(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/Posts/DeletePost?id=${id}`);
  }

  getInbox(platformCode?: string, itemKind?: string): Observable<ApiResponse<InboxItem[]>> {
    const params = new URLSearchParams();
    if (platformCode) params.set('platformCode', platformCode);
    if (itemKind) params.set('itemKind', itemKind);
    const q = params.toString() ? `?${params}` : '';
    return this.http.get<ApiResponse<InboxItem[]>>(`${this.base}/Inbox/GetInbox${q}`);
  }

  replyComment(id: string, message: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base}/Inbox/ReplyToComment?id=${id}`, { message });
  }

  hideComment(id: string, hide: boolean): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base}/Inbox/HideComment?id=${id}`, { hide });
  }

  deleteComment(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/Inbox/DeleteComment?id=${id}`);
  }

  replyMessage(id: string, message: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base}/Inbox/ReplyToMessage?id=${id}`, { message });
  }

  deleteMessage(id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/Inbox/DeleteMessage?id=${id}`);
  }

  markRead(id: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base}/Inbox/MarkRead?id=${id}`, {});
  }

  subscribeWebhook(platformCode: string, callbackUrl?: string): Observable<ApiResponse<object>> {
    const q = callbackUrl ? `?callbackUrl=${encodeURIComponent(callbackUrl)}` : '';
    return this.http.post<ApiResponse<object>>(
      `${this.base}/Webhooks/Subscribe?platformCode=${platformCode}${q ? '&' + q.slice(1) : ''}`,
      {}
    );
  }
}
