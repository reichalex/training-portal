import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { OverviewReport } from '../core/models';
import { TypeIcon } from '../shared/ui';

@Component({
  selector: 'app-overview',
  imports: [RouterLink, TypeIcon],
  template: `
    <div class="page-head row">
      <h1>Admin</h1>
      <div class="actions">
        <a class="btn primary" routerLink="/admin/content/new" [queryParams]="{ type: 'Video' }"
          >Upload a video</a
        >
        <a class="btn" routerLink="/admin/content/new" [queryParams]="{ type: 'Article' }"
          >Write an article</a
        >
        <a class="btn" routerLink="/admin/courses/new">New course</a>
      </div>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (report(); as r) {
      <div class="stats">
        <div class="stat">
          <div class="stat-value">{{ r.learners }}</div>
          <div class="muted small">Active users</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.modules }}</div>
          <div class="muted small">Modules &amp; sections</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.videos }}</div>
          <div class="muted small">Videos</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.articles }}</div>
          <div class="muted small">Articles</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.courses }}</div>
          <div class="muted small">Courses</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.completionsLast30Days }}</div>
          <div class="muted small">Completions, last 30 days</div>
        </div>
        <div class="stat">
          <div class="stat-value">{{ r.activeLearnersLast30Days }}</div>
          <div class="muted small">Active learners, last 30 days</div>
        </div>
      </div>
      <section class="card">
        <h2>Most viewed</h2>
        <table class="table">
          <thead>
            <tr>
              <th></th>
              <th>Title</th>
              <th class="num">Viewers</th>
              <th class="num">Completed</th>
            </tr>
          </thead>
          <tbody>
            @for (t of r.topContent; track t.contentId) {
              <tr>
                <td class="icon-cell"><app-type-icon [type]="t.type" /></td>
                <td>
                  <a [routerLink]="['/admin/reports/content', t.contentId]">{{ t.title }}</a>
                </td>
                <td class="num">{{ t.viewers }}</td>
                <td class="num">{{ t.completions }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="muted">Nobody has watched anything yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }
  `,
})
export class Overview {
  private readonly api = inject(AdminApi);
  protected readonly report = signal<OverviewReport | null>(null);
  protected readonly error = signal('');

  constructor() {
    this.api.overview().then(
      (r) => this.report.set(r),
      (e) => this.error.set(errorMessage(e)),
    );
  }
}
