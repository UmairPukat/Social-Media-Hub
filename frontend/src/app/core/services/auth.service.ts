import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, AuthResponse, LoginRequest, SignupRequest } from '../models/api.models';

const TOKEN_KEY = 'smh_token';
const USER_KEY = 'smh_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = `${environment.apiUrl}/Auth`;

  /** Reactive auth state for the shell layout. */
  readonly isAuthenticated = signal(!!localStorage.getItem(TOKEN_KEY));
  readonly currentUser = signal<{ email: string; fullName: string } | null>(this.readStoredUser());

  constructor(private http: HttpClient, private router: Router) {}

  login(request: LoginRequest): Observable<ApiResponse<AuthResponse>> {
    return this.http.post<ApiResponse<AuthResponse>>(`${this.api}/Login`, request).pipe(
      tap(res => {
        if (res.success && res.data) this.persistSession(res.data);
      })
    );
  }

  signup(request: SignupRequest): Observable<ApiResponse<AuthResponse>> {
    return this.http.post<ApiResponse<AuthResponse>>(`${this.api}/Signup`, request).pipe(
      tap(res => {
        if (res.success && res.data) this.persistSession(res.data);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.isAuthenticated.set(false);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private persistSession(data: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(USER_KEY, JSON.stringify({ email: data.email, fullName: data.fullName }));
    this.isAuthenticated.set(true);
    this.currentUser.set({ email: data.email, fullName: data.fullName });
  }

  private readStoredUser(): { email: string; fullName: string } | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }
}
