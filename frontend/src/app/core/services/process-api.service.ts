import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  ConnectionDetails,
  DashboardSummary,
  InboxItem,
  MetaPage,
  PlatformCard,
  PublishPostResponse,
  SocialAccount,
  SocialPost
} from '../models/api.models';
import { PROCESS_MODULES, PROCESS_MODULE_LIST, ProcessMenuType } from '../config/process.config';

@Injectable({ providedIn: 'root' })
export class ProcessApiService {
  private readonly root = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private base(menuType: ProcessMenuType): string {
    const module = PROCESS_MODULE_LIST.find(m => m.id === menuType)!;
    return `${this.root}/${module.apiBase}`;
  }

  getPlatforms(menuType: ProcessMenuType): Observable<ApiResponse<PlatformCard[]>> {
    return this.http.get<ApiResponse<PlatformCard[]>>(`${this.base(menuType)}/platforms`);
  }

  getAccounts(menuType: ProcessMenuType): Observable<ApiResponse<SocialAccount[]>> {
    return this.http.get<ApiResponse<SocialAccount[]>>(`${this.base(menuType)}/accounts`);
  }

  beginOAuth(menuType: ProcessMenuType, platformCode: string): Observable<ApiResponse<{ authUrl: string; redirectUri: string }>> {
    return this.http.post<ApiResponse<{ authUrl: string; redirectUri: string }>>(
      `${this.base(menuType)}/oauth/begin`,
      { platformCode, menuType }
    );
  }

  getMetaPages(menuType: ProcessMenuType, platformCode: string): Observable<ApiResponse<MetaPage[]>> {
    return this.http.get<ApiResponse<MetaPage[]>>(
      `${this.base(menuType)}/pages?platformCode=${encodeURIComponent(platformCode)}`
    );
  }

  selectMetaPage(menuType: ProcessMenuType, platformCode: string, pageId: string): Observable<ApiResponse<SocialAccount>> {
    return this.http.post<ApiResponse<SocialAccount>>(`${this.base(menuType)}/pages/select`, {
      platformCode,
      pageId,
      menuType
    });
  }

  getConnectionDetails(menuType: ProcessMenuType, platformCode: string): Observable<ApiResponse<ConnectionDetails>> {
    return this.http.get<ApiResponse<ConnectionDetails>>(
      `${this.base(menuType)}/connection-details?platformCode=${encodeURIComponent(platformCode)}`
    );
  }

  disconnect(menuType: ProcessMenuType, platformCode: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(
      `${this.base(menuType)}/disconnect?platformCode=${encodeURIComponent(platformCode)}`,
      {}
    );
  }

  getConfig(menuType: ProcessMenuType, platformCode: string, revealSecret = false): Observable<ApiResponse<unknown>> {
    const q = revealSecret ? '&revealSecret=true' : '';
    return this.http.get<ApiResponse<unknown>>(
      `${this.base(menuType)}/config?platformCode=${encodeURIComponent(platformCode)}${q}`
    );
  }

  saveConfig(menuType: ProcessMenuType, body: Record<string, unknown>): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base(menuType)}/config`, { ...body, menuType });
  }

  deleteConfig(menuType: ProcessMenuType, platformCode: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(
      `${this.base(menuType)}/config?platformCode=${encodeURIComponent(platformCode)}`
    );
  }

  getPosts(menuType: ProcessMenuType, platformId?: string): Observable<ApiResponse<SocialPost[]>> {
    const q = platformId ? `?platformId=${platformId}` : '';
    return this.http.get<ApiResponse<SocialPost[]>>(`${this.base(menuType)}/posts${q}`);
  }

  createPost(menuType: ProcessMenuType, body: unknown): Observable<ApiResponse<PublishPostResponse>> {
    return this.http.post<ApiResponse<PublishPostResponse>>(`${this.base(menuType)}/posts`, body);
  }

  deletePost(menuType: ProcessMenuType, id: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.base(menuType)}/posts/${id}`);
  }

  getInbox(menuType: ProcessMenuType, platformCode?: string, itemKind?: string): Observable<ApiResponse<InboxItem[]>> {
    const params = new URLSearchParams();
    if (platformCode) params.set('platformCode', platformCode);
    if (itemKind) params.set('itemKind', itemKind);
    const q = params.toString() ? `?${params}` : '';
    return this.http.get<ApiResponse<InboxItem[]>>(`${this.base(menuType)}/inbox${q}`);
  }

  replyComment(
    menuType: ProcessMenuType,
    id: string,
    message: string,
    routing?: { pageId?: string; accountId?: string }
  ): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base(menuType)}/inbox/comments/${id}/reply`, {
      message,
      menuType,
      pageId: routing?.pageId,
      accountId: routing?.accountId
    });
  }

  replyMessage(
    menuType: ProcessMenuType,
    id: string,
    message: string,
    replyToMessageId?: string,
    routing?: { pageId?: string; accountId?: string }
  ): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base(menuType)}/inbox/messages/${id}/reply`, {
      message,
      replyToMessageId: replyToMessageId ?? null,
      menuType,
      pageId: routing?.pageId,
      accountId: routing?.accountId
    });
  }

  markRead(menuType: ProcessMenuType, id: string): Observable<ApiResponse<object>> {
    return this.http.post<ApiResponse<object>>(`${this.base(menuType)}/inbox/conversations/${id}/read`, {});
  }

  getDashboardSummary(menuType: ProcessMenuType): Observable<ApiResponse<DashboardSummary>> {
    return this.http.get<ApiResponse<DashboardSummary>>(`${this.base(menuType)}/analytics/summary`);
  }
}
