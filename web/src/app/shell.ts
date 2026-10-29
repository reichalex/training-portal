import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  template: `
    <header class="topbar">
      <div class="topbar-inner">
        <a routerLink="/" class="brand">
          <span class="brand-mark" aria-hidden="true">▶</span>
          <span>Training Portal</span>
        </a>
        <button
          class="menu-toggle"
          type="button"
          (click)="menuOpen.set(!menuOpen())"
          aria-label="Menu"
        >
          ☰
        </button>
        <nav class="mainnav" [class.open]="menuOpen()" (click)="menuOpen.set(false)">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"
            >Home</a
          >
          <a routerLink="/browse" routerLinkActive="active">Browse</a>
          <a routerLink="/courses" routerLinkActive="active">Courses</a>
          @if (auth.isAdmin()) {
            <a routerLink="/admin" routerLinkActive="active">Admin</a>
          }
        </nav>
        <form class="search" (ngSubmit)="search()" role="search">
          <input
            type="search"
            name="q"
            [(ngModel)]="query"
            placeholder="Search training…"
            aria-label="Search training"
          />
        </form>
        <div class="usermenu">
          <a routerLink="/account" class="avatar" [title]="auth.user()?.displayName">{{
            initials()
          }}</a>
          <button type="button" class="link" (click)="auth.logout()">Sign out</button>
        </div>
      </div>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly menuOpen = signal(false);
  protected query = '';

  protected initials(): string {
    const name = this.auth.user()?.displayName ?? '';
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0].toUpperCase())
      .join('');
  }

  protected search(): void {
    const q = this.query.trim();
    if (q) this.router.navigate(['/search'], { queryParams: { q } });
  }
}
