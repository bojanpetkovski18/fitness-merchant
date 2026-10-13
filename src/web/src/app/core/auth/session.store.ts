import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SessionUser {
  readonly id: string;
  readonly email: string;
  readonly displayName: string;
  readonly roles: readonly string[];
}

interface SessionResponse {
  readonly user: SessionUser | null;
}

export interface RegisterInput {
  readonly email: string;
  readonly displayName: string;
  readonly password: string;
}

/**
 * The signed-in user, shared by every screen (D006). Loaded once before the first route renders, so guards
 * never see an "unknown" state. The session itself is an HttpOnly cookie; nothing is kept in storage.
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly http = inject(HttpClient);
  private readonly currentUser = signal<SessionUser | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isSignedIn = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.roles.includes('Admin') ?? false);

  /** Never throws: if the API is unreachable the app still starts, as a guest. */
  async load(): Promise<void> {
    try {
      const response = await firstValueFrom(this.http.get<SessionResponse>('/api/auth/me'));
      this.currentUser.set(response.user);
    } catch {
      this.currentUser.set(null);
    }
  }

  async signIn(email: string, password: string): Promise<SessionUser | null> {
    const response = await firstValueFrom(
      this.http.post<SessionResponse>('/api/auth/sign-in', { email, password }),
    );
    this.currentUser.set(response.user);
    return response.user;
  }

  async register(input: RegisterInput): Promise<SessionUser | null> {
    const response = await firstValueFrom(
      this.http.post<SessionResponse>('/api/auth/register', input),
    );
    this.currentUser.set(response.user);
    return response.user;
  }

  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/sign-out', null));
    } finally {
      this.currentUser.set(null);
    }
  }

  /** The server says the session is gone (expired, signed out elsewhere). */
  clear(): void {
    this.currentUser.set(null);
  }
}
