import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  AppConnectionConfig,
  ConnectionDetails,
  DashboardSummary,
  InboxItem,
  MENU_TYPES,
  MenuType,
  MetaPage,
  PlatformCard,
  PublishPostResponse,
  SaveAppConnectionConfigRequest,
  SocialAccount,
  SocialPost
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private withMenuType(params: URLSearchParams, menuType?: MenuType): URLSearchParams {
    params.set('menuType', menuType ?? MENU_TYPES.integration);
    return params;
  }

  getDashboard(): Observable<ApiResponse<DashboardSummary>> {
    return this.http.get<ApiResponse<DashboardSummary>>(`${this.base}/Dashboard/GetSummary`);
  }

  getPlatforms(menuType: MenuType = MENU_TYPES.integration): Observable<ApiResponse<PlatformCard[]>> {
    const params = this.withMenuType(new URLSearchParams(), menuType);
    return this.http.get<ApiResponse<PlatformCard[]>>(`${this.base}/SocialAccounts/GetPlatformCards?${params}`);
  }

  getAccounts(menuType: MenuType = MENU_TYPES.integration): Observable<ApiResponse<SocialAccount[]>> {
    const params = this.withMenuType(new URLSearchParams(), menuType);
    return this.http.get<ApiResponse<SocialAccount[]>>(`${this.base}/SocialAccounts/GetConnectedAccounts?${params}`);
  }

  getMetaPages(platformCode: string, menuType: MenuType = MENU_TYPES.integration): Observable<ApiResponse<MetaPage[]>> {
    const params = this.withMenuType(new URLSearchParams({ platformCode }), menuType);
    return this.http.get<ApiResponse<MetaPage[]>>(`${this.base}/Integrations/GetPages?${params}`);
  }

  selectMetaPage(
    platformCode: string,
    pageId: string,
    menuType: MenuType = MENU_TYPES.integration
  ): Observable<ApiResponse<SocialAccount>> {
    return this.http.post<ApiResponse<SocialAccount>>(`${this.base}/Integrations/SelectPage`, {
      platformCode,
      pageId,
      menuType
    });
  }

  getConnectionDetails(
    platformCode: string,
    menuType: MenuType = MENU_TYPES.integration
  ): Observable<ApiResponse<ConnectionDetails>> {
    const params = this.withMenuType(new URLSearchParams({ platformCode }), menuType);
    return this.http.get<ApiResponse<ConnectionDetails>>(`${this.base}/Integrations/GetConnectionDetails?${params}`);
  }

  disconnect(platformCode: string, menuType: MenuType = MENU_TYPES.integration): Observable<ApiResponse<object>> {
    const params = this.withMenuType(new URLSearchParams({ platformCode }), menuType);
    return this.http.post<ApiResponse<object>>(`${this.base}/SocialAccounts/Disconnect?${params}`, {});
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

  replyMessage(id: string, message: string, replyToMessageId?: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base}/Inbox/ReplyToMessage?id=${id}`, {
      message,
      replyToMessageId: replyToMessageId ?? null
    });
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

  getAppConnectionConfig(
    platformCode: string,
    menuType: MenuType = MENU_TYPES.appConnection,
    revealSecret = false
  ): Observable<ApiResponse<AppConnectionConfig>> {
    const params = this.withMenuType(new URLSearchParams({ platformCode }), menuType);
    if (revealSecret) params.set('revealSecret', 'true');
    return this.http.get<ApiResponse<AppConnectionConfig>>(`${this.base}/AppConnections/GetConfig?${params}`);
  }

  saveAppConnectionConfig(body: SaveAppConnectionConfigRequest): Observable<ApiResponse<AppConnectionConfig>> {
    return this.http.post<ApiResponse<AppConnectionConfig>>(`${this.base}/AppConnections/SaveConfig`, {
      ...body,
      menuType: body.menuType ?? MENU_TYPES.appConnection
    });
  }

  deleteAppConnectionConfig(
    platformCode: string,
    menuType: MenuType = MENU_TYPES.appConnection
  ): Observable<ApiResponse<object>> {
    const params = this.withMenuType(new URLSearchParams({ platformCode }), menuType);
    return this.http.delete<ApiResponse<object>>(`${this.base}/AppConnections/DeleteConfig?${params}`);
  }
}
