import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface UserProfileDto {
  id: string;
  email: string;
  fullName: string;
  phone: string;
  linkedInUrl: string;
  gitHubUrl: string;
  address: string;
  role?: string;
}

export interface AuthResponse {
  success: boolean;
  token: string;
  message: string;
  user?: UserProfileDto;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = '/api/auth';
  private tokenKey = 'maxerz_auth_token';

  currentUser = signal<UserProfileDto | null>(null);
  isLoggedIn = signal<boolean>(false);

  isAdmin(): boolean {
    return this.currentUser()?.role === 'Admin';
  }

  constructor(private http: HttpClient) {
    this.checkInitialAuth();
  }

  private checkInitialAuth() {
    const token = this.getToken();
    if (token) {
      this.fetchCurrentUser().subscribe({
        next: (user) => {
          this.currentUser.set(user);
          this.isLoggedIn.set(true);
        },
        error: () => {
          this.logout();
        }
      });
    }
  }

  register(req: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, req).pipe(
      tap((res) => {
        if (res.success && res.token) {
          this.saveToken(res.token);
          if (res.user) this.currentUser.set(res.user);
          this.isLoggedIn.set(true);
        }
      })
    );
  }

  login(req: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, req).pipe(
      tap((res) => {
        if (res.success && res.token) {
          this.saveToken(res.token);
          if (res.user) this.currentUser.set(res.user);
          this.isLoggedIn.set(true);
        }
      })
    );
  }

  loginWithGoogle(payload: { email: string; fullName: string; idToken?: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/google`, payload).pipe(
      tap((res) => {
        if (res.success && res.token) {
          this.saveToken(res.token);
          if (res.user) this.currentUser.set(res.user);
          this.isLoggedIn.set(true);
        }
      })
    );
  }

  fetchCurrentUser(): Observable<UserProfileDto> {
    const headers = { Authorization: `Bearer ${this.getToken()}` };
    return this.http.get<UserProfileDto>(`${this.apiUrl}/me`, { headers });
  }

  updateProfile(dto: Partial<UserProfileDto>): Observable<UserProfileDto> {
    const headers = { Authorization: `Bearer ${this.getToken()}` };
    return this.http.put<UserProfileDto>(`${this.apiUrl}/profile`, dto, { headers }).pipe(
      tap((updated) => {
        this.currentUser.set(updated);
      })
    );
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    this.currentUser.set(null);
    this.isLoggedIn.set(false);
  }

  saveToken(token: string) {
    localStorage.setItem(this.tokenKey, token);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }
}
