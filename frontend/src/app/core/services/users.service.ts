import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminResetPasswordRequest, ApiResponse, UserListItem } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly base = `${environment.apiUrl}/Users`;

  constructor(private http: HttpClient) {}

  list(): Observable<ApiResponse<UserListItem[]>> {
    return this.http.get<ApiResponse<UserListItem[]>>(this.base);
  }

  resetPassword(userId: string, body: AdminResetPasswordRequest): Observable<ApiResponse<object>> {
    return this.http.put<ApiResponse<object>>(`${this.base}/${userId}/reset-password`, body);
  }

  delete(userId: string): Observable<ApiResponse<object>> {
    return this.http.delete<ApiResponse<object>>(`${this.base}/${userId}`);
  }
}
