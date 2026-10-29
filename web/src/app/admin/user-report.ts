import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { UserReportDetail } from '../core/models';
import { DurationPipe, ProgressBar, TypeIcon } from '../shared/ui';

@Component({
  selector: 'app-user-report',
  imports: [RouterLink, DatePipe, DurationPipe, ProgressBar, TypeIcon],
  template: `
    <nav class="breadcrumb"><a routerLink="/admin/reports">Reports</a></nav>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (report(); as r) {
      <div class="page-head">
        <h1>{{ r.user.displayName }}</h1>
        <p class="muted">
          {{ r.user.email }}{{ r.user.department ? ' · ' + r.user.department : '' }}
        </p>
      </div>

      <section class="card flush">
        <h2 class="pad">Courses</h2>
        <table class="table">
          <thead>
            <tr>
              <th>Course</th>
              <th>Progress</th>
              <th>Due</th>
              <th>Completed</th>
            </tr>
          </thead>
          <tbody>
            @for (c of r.courses; track c.id) {
              <tr>
                <td>
                  <a [routerLink]="['/admin/courses', c.id]">{{ c.title }}</a
                  >{{ c.assigned ? '' : ' (self-enrolled)' }}
                </td>
                <td class="progress-cell">
                  <app-progress [value]="c.percent" /><span class="small muted"
                    >{{ c.completedItems }}/{{ c.itemCount }}</span
                  >
                </td>
                <td class="small">{{ c.dueDate ? (c.dueDate | date: 'mediumDate') : '' }}</td>
                <td class="small">
                  {{ c.completedAt ? (c.completedAt | date: 'mediumDate') : '' }}
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="muted">Not enrolled in any course.</td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="card flush">
        <h2 class="pad">Videos &amp; articles</h2>
        <table class="table">
          <thead>
            <tr>
              <th></th>
              <th>Title</th>
              <th>Progress</th>
              <th class="num">Time watched</th>
              <th>First viewed</th>
              <th>Completed</th>
            </tr>
          </thead>
          <tbody>
            @for (c of r.content; track c.contentId) {
              <tr>
                <td class="icon-cell"><app-type-icon [type]="c.type" /></td>
                <td>
                  <a [routerLink]="['/admin/reports/content', c.contentId]">{{ c.title }}</a>
                  <div class="muted small">{{ c.modulePath }}</div>
                </td>
                <td class="progress-cell">
                  <app-progress [value]="c.percent" /><span class="small muted"
                    >{{ c.percent }}%</span
                  >
                </td>
                <td class="num small">
                  {{ c.type === 'Video' ? (c.secondsWatched | duration) : '' }}
                </td>
                <td class="small">{{ c.firstViewedAt | date: 'medium' }}</td>
                <td class="small">{{ c.completedAt ? (c.completedAt | date: 'medium') : '' }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="muted">Has not opened any training yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }
  `,
})
export class UserReport {
  private readonly api = inject(AdminApi);
  readonly id = input.required<string>();
  protected readonly report = signal<UserReportDetail | null>(null);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      this.api.userReport(Number(this.id())).then(
        (r) => this.report.set(r),
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }
}
