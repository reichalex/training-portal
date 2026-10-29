import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { UserReportRow } from '../core/models';

type SortKey =
  | 'displayName'
  | 'itemsCompleted'
  | 'coursesCompleted'
  | 'coursesOverdue'
  | 'minutesWatched'
  | 'lastActivityAt';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="page-head row">
      <div>
        <h1>Reports</h1>
        <p class="muted">
          Who has watched and read what. Open a person for their full training record, or export
          everything to Excel.
        </p>
      </div>
      <div class="actions">
        <button class="btn" type="button" (click)="download('content')">
          Export viewing (CSV)
        </button>
        <button class="btn" type="button" (click)="download('courses')">
          Export courses (CSV)
        </button>
      </div>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    <div class="filters">
      <input
        type="search"
        placeholder="Filter by name, email or department…"
        [ngModel]="filter()"
        (ngModelChange)="filter.set($event)"
        aria-label="Filter"
      />
      <label class="check"
        ><input
          type="checkbox"
          [ngModel]="overdueOnly()"
          (ngModelChange)="overdueOnly.set($event)"
        />
        Only people with overdue courses</label
      >
    </div>
    <div class="card flush">
      <table class="table">
        <thead>
          <tr>
            <th>
              <button class="sort" type="button" (click)="sortBy('displayName')">Name</button>
            </th>
            <th>Department</th>
            <th class="num">Started</th>
            <th class="num">
              <button class="sort" type="button" (click)="sortBy('itemsCompleted')">
                Completed
              </button>
            </th>
            <th class="num">
              <button class="sort" type="button" (click)="sortBy('coursesCompleted')">
                Courses done
              </button>
            </th>
            <th class="num">
              <button class="sort" type="button" (click)="sortBy('coursesOverdue')">Overdue</button>
            </th>
            <th class="num">
              <button class="sort" type="button" (click)="sortBy('minutesWatched')">Minutes</button>
            </th>
            <th>
              <button class="sort" type="button" (click)="sortBy('lastActivityAt')">
                Last activity
              </button>
            </th>
          </tr>
        </thead>
        <tbody>
          @for (r of shown(); track r.userId) {
            <tr>
              <td>
                <a [routerLink]="['/admin/reports/users', r.userId]"
                  ><strong>{{ r.displayName }}</strong></a
                >
                <div class="muted small">{{ r.email }}</div>
              </td>
              <td>{{ r.department }}</td>
              <td class="num">{{ r.itemsStarted }}</td>
              <td class="num">{{ r.itemsCompleted }}</td>
              <td class="num">{{ r.coursesCompleted }} / {{ r.coursesEnrolled }}</td>
              <td class="num" [class.overdue]="r.coursesOverdue > 0">
                {{ r.coursesOverdue || '' }}
              </td>
              <td class="num">{{ r.minutesWatched }}</td>
              <td class="small muted">
                {{ r.lastActivityAt ? (r.lastActivityAt | date: 'medium') : 'Never' }}
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="8" class="muted">Nobody matches.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class Reports {
  private readonly api = inject(AdminApi);
  protected readonly rows = signal<UserReportRow[]>([]);
  protected readonly filter = signal('');
  protected readonly overdueOnly = signal(false);
  protected readonly sort = signal<{ key: SortKey; desc: boolean }>({
    key: 'displayName',
    desc: false,
  });
  protected readonly error = signal('');

  protected readonly shown = computed(() => {
    const t = this.filter().trim().toLowerCase();
    const { key, desc } = this.sort();
    const rows = this.rows().filter(
      (r) =>
        (!t || `${r.displayName} ${r.email} ${r.department ?? ''}`.toLowerCase().includes(t)) &&
        (!this.overdueOnly() || r.coursesOverdue > 0),
    );
    return [...rows].sort((a, b) => {
      const x = a[key] ?? '';
      const y = b[key] ?? '';
      const cmp =
        typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y));
      return desc ? -cmp : cmp;
    });
  });

  constructor() {
    this.api.userReports().then(
      (r) => this.rows.set(r),
      (e) => this.error.set(errorMessage(e)),
    );
  }

  protected sortBy(key: SortKey): void {
    const current = this.sort();
    // Names sort A–Z first; numbers and dates sort largest / most recent first.
    this.sort.set({ key, desc: current.key === key ? !current.desc : key !== 'displayName' });
  }

  protected async download(kind: 'content' | 'courses'): Promise<void> {
    try {
      await this.api.download(kind);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
