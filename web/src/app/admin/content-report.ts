import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { ContentReport } from '../core/models';
import { DurationPipe, ProgressBar } from '../shared/ui';

@Component({
  selector: 'app-content-report',
  imports: [RouterLink, DatePipe, DurationPipe, ProgressBar],
  template: `
    <nav class="breadcrumb"><a routerLink="/admin/content">Videos &amp; articles</a></nav>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (report(); as r) {
      <div class="page-head row">
        <div>
          <h1>{{ r.title }}</h1>
          <p class="muted">
            {{ r.rows.length }} {{ r.rows.length === 1 ? 'person has' : 'people have' }} opened this
            {{ r.type === 'Video' ? 'video' : 'article' }}; {{ completedCount() }} completed it.
          </p>
        </div>
        <a class="btn" [routerLink]="['/admin/content', r.contentId]">Edit</a>
      </div>
      <div class="card flush">
        <table class="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Department</th>
              <th>Progress</th>
              <th class="num">Time watched</th>
              <th>First viewed</th>
              <th>Last viewed</th>
              <th>Completed</th>
            </tr>
          </thead>
          <tbody>
            @for (row of r.rows; track row.userId) {
              <tr>
                <td>
                  <a [routerLink]="['/admin/reports/users', row.userId]">{{ row.displayName }}</a>
                  <div class="muted small">{{ row.email }}</div>
                </td>
                <td>{{ row.department }}</td>
                <td class="progress-cell">
                  <app-progress [value]="row.percent" /><span class="small muted"
                    >{{ row.percent }}%</span
                  >
                </td>
                <td class="num small">
                  {{ r.type === 'Video' ? (row.secondsWatched | duration) : '' }}
                </td>
                <td class="small">{{ row.firstViewedAt | date: 'medium' }}</td>
                <td class="small">{{ row.lastViewedAt | date: 'medium' }}</td>
                <td class="small">
                  {{ row.completedAt ? (row.completedAt | date: 'medium') : '' }}
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="muted">Nobody has opened this yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class ContentReportPage {
  private readonly api = inject(AdminApi);
  readonly id = input.required<string>();
  protected readonly report = signal<ContentReport | null>(null);
  protected readonly error = signal('');
  protected readonly completedCount = computed(
    () => this.report()?.rows.filter((r) => r.isCompleted).length ?? 0,
  );

  constructor() {
    effect(() => {
      this.api.contentReport(Number(this.id())).then(
        (r) => this.report.set(r),
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }
}
