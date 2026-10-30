import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import type { IPublicClientApplication } from '@azure/msal-browser';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { AuthConfig, LoginResponse, User } from './models';

const STORAGE_KEY = 'training-portal.session';

interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(AuthService.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  private configPromise: Promise<AuthConfig> | null = null;
  private msalClient: Promise<IPublicClientApplication> | null = null;

  /** Which sign-in methods the server offers (fetched once). */
  config(): Promise<AuthConfig> {
    this.configPromise ??= firstValueFrom(this.http.get<AuthConfig>('/api/auth/config')).catch(
      (e) => {
        this.configPromise = null;
        throw e;
      },
    );
    return this.configPromise;
  }

  /** Sends the browser to Microsoft; it comes back to /login, where completeMicrosoftLogin finishes. */
  async loginWithMicrosoft(returnUrl: string): Promise<void> {
    const { client, scope } = await this.msal();
    await client.loginRedirect({ scopes: [scope], state: returnUrl, prompt: 'select_account' });
  }

  /**
   * On the way back from Microsoft: exchanges the Microsoft token for a portal session.
   * Returns the page to continue to, or null if this page load is not a sign-in redirect.
   */
  async completeMicrosoftLogin(): Promise<string | null> {
    const config = await this.config();
    if (!config.entra) return null;
    const { client } = await this.msal();
    const result = await client.handleRedirectPromise();
    if (!result) return null;
    const res = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/entra', { accessToken: result.accessToken }),
    );
    this.store(res);
    return result.state || '/';
  }

  /** Microsoft's sign-in library, loaded only when Microsoft sign-in is configured. */
  private async msal(): Promise<{ client: IPublicClientApplication; scope: string }> {
    const entra = (await this.config()).entra;
    if (!entra) throw new Error('Microsoft sign-in is not configured.');
    this.msalClient ??= import('@azure/msal-browser').then((m) =>
      m.createStandardPublicClientApplication({
        auth: {
          clientId: entra.clientId,
          authority: entra.authority,
          redirectUri: `${location.origin}/login`,
        },
        cache: { cacheLocation: 'sessionStorage' },
      }),
    );
    return { client: await this.msalClient, scope: entra.scope };
  }

  async login(email: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/login', { email, password }),
    );
    this.store(res);
  }

  logout(redirect = true): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    if (redirect) this.router.navigate(['/login']);
  }

  /** Refreshes the stored user (role, name) from the server. */
  async refresh(): Promise<void> {
    const s = this.session();
    if (!s) return;
    const user = await firstValueFrom(this.http.get<User>('/api/auth/me'));
    this.store({ ...s, user });
  }

  private store(s: Session): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
    this.session.set(s);
  }

  private static restore(): Session | null {
    try {
      const s = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
      return s && new Date(s.expiresAt) > new Date() ? s : null;
    } catch {
      return null;
    }
  }
}

/** Adds the login token to API calls and sends the user to the login page when it expires. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token;
  const authed =
    token && req.url.startsWith('/api/')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;
  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      // Sign-in failures are shown on the login page itself.
      const signingIn = req.url.endsWith('/auth/login') || req.url.endsWith('/auth/entra');
      if (err.status === 401 && !signingIn) {
        auth.logout(false);
        router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => err);
    }),
  );
};

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.user()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAdmin() ? true : inject(Router).createUrlTree(['/']);
};

/** The message from an API error, for showing to the user. */
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as {
      message?: string;
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    } | null;
    if (body?.message) return body.message;
    if (body?.errors) return Object.values(body.errors).flat().join(' ');
    if (body?.detail) return body.detail;
    if (body?.title) return body.title;
    if (err.status === 0) return 'Cannot reach the server.';
    return `Request failed (${err.status}).`;
  }
  // Errors from Microsoft's sign-in library carry a readable message (e.g. an AADSTS code).
  if (err instanceof Error && err.message) return err.message;
  return 'Something went wrong.';
}
