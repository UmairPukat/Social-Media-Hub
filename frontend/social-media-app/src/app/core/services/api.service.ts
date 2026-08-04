import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  InboxItem,
  InboxItemType,
  PlatformCard,
  SocialPlatform,
  SocialPost
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  getPlatforms(): Observable<ApiResponse<PlatformCard[]>> {
    return this.http.get<ApiResponse<PlatformCard[]>>(`${environment.apiUrl}/integrations/platforms`);
  }

  disconnect(platform: SocialPlatform): Observable<ApiResponse<string>> {
    return this.http.delete<ApiResponse<string>>(`${environment.apiUrl}/integrations/${platform}`);
  }

  getAuthUrl(platform: 'facebook' | 'instagram' | 'whatsapp'): Observable<ApiResponse<{ authorizationUrl: string; state: string }>> {
    return this.http.get<ApiResponse<{ authorizationUrl: string; state: string }>>(`${environment.apiUrl}/${platform}/auth-url`);
  }

  createPost(payload: { platform: SocialPlatform; content: string; mediaUrl?: string; publishNow: boolean }): Observable<ApiResponse<SocialPost>> {
    return this.http.post<ApiResponse<SocialPost>>(`${environment.apiUrl}/posts`, payload);
  }

  getPosts(platform?: SocialPlatform): Observable<ApiResponse<SocialPost[]>> {
    let params = new HttpParams();
    if (platform != null) params = params.set('platform', platform);
    return this.http.get<ApiResponse<SocialPost[]>>(`${environment.apiUrl}/posts`, { params });
  }

  getInbox(platform?: SocialPlatform | null, itemType?: InboxItemType | null): Observable<ApiResponse<InboxItem[]>> {
    let params = new HttpParams();
    if (platform != null) params = params.set('platform', platform);
    if (itemType != null) params = params.set('itemType', itemType);
    return this.http.get<ApiResponse<InboxItem[]>>(`${environment.apiUrl}/inbox`, { params });
  }

  reply(id: string, message: string): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${environment.apiUrl}/inbox/${id}/reply`, { message });
  }

  hideComment(id: string): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${environment.apiUrl}/inbox/${id}/hide`, {});
  }

  deleteInboxItem(id: string): Observable<ApiResponse<string>> {
    return this.http.delete<ApiResponse<string>>(`${environment.apiUrl}/inbox/${id}`);
  }

  completeOAuth(platform: string, code: string, state: string): Observable<ApiResponse<string>> {
    return this.http.get<ApiResponse<string>>(`${environment.apiUrl}/${platform}/callback`, {
      params: { code, state }
    });
  }
}
