import { Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, errorMessage } from '../core/auth';
import { AuthConfig } from '../core/models';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="login-wrap">
      <div class="card login">
        <div class="brand big">
          <span class="brand-mark" aria-hidden="true">▶</span> Training Portal
        </div>
        <p class="muted">Sign in to watch training videos and track your courses.</p>

        @if (!config()) {
          @if (!error()) {
            <p class="muted">Signing you in…</p>
          }
        } @else {
          @if (config()?.entra) {
            <button
              class="btn microsoft block"
              type="button"
              (click)="microsoft()"
              [disabled]="busy()"
            >
              <svg width="18" height="18" viewBox="0 0 21 21" aria-hidden="true">
                <rect x="1" y="1" width="9" height="9" fill="#f25022" />
                <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
                <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
                <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
              </svg>
              Sign in with Microsoft
            </button>
          }
          @if (config()?.entra && config()?.localLogin) {
            <div class="divider"><span>or use a portal password</span></div>
          }
          @if (config()?.localLogin) {
            <form (ngSubmit)="submit()">
              <label>
                Email
                <input
                  type="email"
                  name="email"
                  [(ngModel)]="email"
                  required
                  autocomplete="username"
                />
              </label>
              <label>
                Password
                <input
                  type="password"
                  name="password"
                  [(ngModel)]="password"
                  required
                  autocomplete="current-password"
                />
              </label>
              <button
                class="btn block"
                [class.primary]="!config()?.entra"
                type="submit"
                [disabled]="busy()"
              >
                {{ busy() ? 'Signing in…' : 'Sign in' }}
              </button>
            </form>
          }
        }
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
      </div>
    </div>
  `,
})
export class Login implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly returnUrl = input<string>();
  protected email = '';
  protected password = '';
  protected readonly config = signal<AuthConfig | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      // Coming back from Microsoft: finish signing in and continue where the person was going.
      const next = await this.auth.completeMicrosoftLogin();
      if (next) {
        this.router.navigateByUrl(next);
        return;
      }
    } catch (err) {
      this.error.set(errorMessage(err));
    }
    try {
      this.config.set(await this.auth.config());
    } catch (err) {
      this.error.set(errorMessage(err));
    }
  }

  protected async microsoft(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.loginWithMicrosoft(this.returnUrl() || '/');
    } catch (err) {
      this.error.set(errorMessage(err));
      this.busy.set(false);
    }
  }

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.email, this.password);
      this.router.navigateByUrl(this.returnUrl() || '/');
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
