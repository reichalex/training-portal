import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService, errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { Dashboard } from '../core/models';
import { ContentList, CourseCard } from '../shared/cards';

@Component({
  selector: 'app-home',
  imports: [RouterLink, ContentList, CourseCard],
  template: `
    <div class="page-head">
      <h1>Welcome back, {{ firstName() }}</h1>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (data(); as d) {
      <div class="stats">
        <div class="stat">
          <div class="stat-value">{{ d.stats.completedItems }}</div>
          <div class="muted small">Videos &amp; articles completed</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ d.stats.completedCourses }}</div>
          <div class="muted small">Courses completed</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ d.stats.minutesWatched }}</div>
          <div class="muted small">Minutes watched</div>
        </div>
      </div>

      @if (d.assignedCourses.length) {
        <section>
          <h2>Assigned to you</h2>
          <div class="grid">
            @for (c of d.assignedCourses; track c.id) {
              <app-course-card [course]="c" />
            }
          </div>
        </section>
      }
      @if (d.inProgressCourses.length) {
        <section>
          <h2>Your courses</h2>
          <div class="grid">
            @for (c of d.inProgressCourses; track c.id) {
              <app-course-card [course]="c" />
            }
          </div>
        </section>
      }
      <section>
        <h2>Continue where you left off</h2>
        <app-content-list [items]="d.continueWatching" emptyText="Nothing in progress." />
      </section>
      @if (d.recentlyCompleted.length) {
        <section>
          <h2>Recently completed</h2>
          <app-content-list [items]="d.recentlyCompleted" />
        </section>
      }
      @if (!d.assignedCourses.length && !d.inProgressCourses.length && !d.continueWatching.length) {
        <div class="card empty-state">
          <h3>Ready to learn?</h3>
          <p class="muted">Browse training by topic, or start a course.</p>
          <a class="btn primary" routerLink="/browse">Browse training</a>
          <a class="btn" routerLink="/courses">See courses</a>
        </div>
      }
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
})
export class Home {
  private readonly api = inject(LearnerApi);
  private readonly auth = inject(AuthService);
  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal('');

  constructor() {
    this.api.dashboard().then(
      (d) => this.data.set(d),
      (e) => this.error.set(errorMessage(e)),
    );
  }

  protected firstName(): string {
    return this.auth.user()?.displayName.split(' ')[0] ?? '';
  }
}
