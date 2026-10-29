import { DatePipe } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import {
  AdminContent,
  AdminCourse,
  AdminCourseItem,
  AdminModule,
  CourseReport,
  User,
} from '../core/models';
import { ProgressBar, TypeIcon } from '../shared/ui';

@Component({
  selector: 'app-course-edit',
  imports: [FormsModule, RouterLink, DatePipe, TypeIcon, ProgressBar],
  template: `
    <nav class="breadcrumb"><a routerLink="/admin/courses">Courses</a></nav>
    <div class="page-head row">
      <h1>{{ existing() ? existing()?.title : 'New course' }}</h1>
      @if (existing(); as c) {
        <a class="btn" [routerLink]="['/courses', c.id]">View as learner</a>
      }
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (!ready()) {
      <p class="muted">Loading…</p>
    } @else {
      <form class="card form" (ngSubmit)="save()">
        <div class="form-grid">
          <label class="span-2">
            Title
            <input name="title" [(ngModel)]="title" required maxlength="300" />
          </label>
          <label class="span-2">
            Description
            <textarea
              name="description"
              [(ngModel)]="description"
              rows="3"
              maxlength="4000"
            ></textarea>
          </label>
          <label>
            Show under module
            <select name="module" [(ngModel)]="moduleId">
              <option [ngValue]="null">— Courses page only —</option>
              @for (m of modules(); track m.id) {
                <option [ngValue]="m.id">{{ m.path }}</option>
              }
            </select>
          </label>
          <div class="checks">
            <label class="check"
              ><input type="checkbox" name="enforce" [(ngModel)]="enforceOrder" /> Steps must be
              completed in order</label
            >
            <label class="check"
              ><input type="checkbox" name="published" [(ngModel)]="isPublished" /> Published
              (visible to learners)</label
            >
          </div>
        </div>

        <fieldset>
          <legend>Steps ({{ steps().length }})</legend>
          <ol class="step-editor">
            @for (s of steps(); track s.contentId; let i = $index, first = $first, last = $last) {
              <li>
                <span class="step-num">{{ i + 1 }}</span>
                <app-type-icon [type]="s.type" />
                <div class="grow">
                  <div>{{ s.title }}</div>
                  <div class="muted small">
                    {{ s.modulePath }}{{ s.isPublished ? '' : ' · draft (hidden from learners)' }}
                  </div>
                </div>
                <button
                  class="icon-btn"
                  type="button"
                  (click)="move(i, -1)"
                  [disabled]="first"
                  title="Move up"
                >
                  ↑
                </button>
                <button
                  class="icon-btn"
                  type="button"
                  (click)="move(i, 1)"
                  [disabled]="last"
                  title="Move down"
                >
                  ↓
                </button>
                <button
                  class="icon-btn danger"
                  type="button"
                  (click)="removeStep(i)"
                  title="Remove"
                >
                  ✕
                </button>
              </li>
            } @empty {
              <li class="muted">No steps yet. Add videos and articles below.</li>
            }
          </ol>
          <div class="picker">
            <input
              type="search"
              placeholder="Find a video or article to add…"
              [ngModel]="pickerText()"
              (ngModelChange)="pickerText.set($event)"
              name="picker"
            />
            @if (pickerText().trim()) {
              <ul class="picker-results">
                @for (c of pickerResults(); track c.id) {
                  <li>
                    <button type="button" (click)="addStep(c)">
                      <app-type-icon [type]="c.type" />
                      <span class="grow"
                        >{{ c.title }} <span class="muted small">· {{ c.modulePath }}</span></span
                      >
                      <span class="link">Add</span>
                    </button>
                  </li>
                } @empty {
                  <li class="muted small">No matching content.</li>
                }
              </ul>
            }
          </div>
        </fieldset>

        <div class="actions">
          <button class="btn primary" type="submit" [disabled]="busy() || !title.trim()">
            {{ busy() ? 'Saving…' : 'Save course' }}
          </button>
          <a class="btn" routerLink="/admin/courses">Cancel</a>
          @if (existing()) {
            <span class="spacer"></span>
            <button class="btn danger" type="button" (click)="remove()">Delete course</button>
          }
        </div>
        @if (saved()) {
          <p class="success-text">Saved.</p>
        }
      </form>
    }

    @if (existing(); as c) {
      <section class="card">
        <h2>Assign to people</h2>
        <p class="muted small">
          Assigned courses appear at the top of each person's home page, with the due date.
        </p>
        <div class="assign">
          <input
            type="search"
            placeholder="Filter people…"
            [ngModel]="userFilter()"
            (ngModelChange)="userFilter.set($event)"
            name="userFilter"
          />
          <div class="user-picks">
            @for (u of filteredUsers(); track u.id) {
              <label class="check">
                <input type="checkbox" [checked]="selected().has(u.id)" (change)="toggle(u.id)" />
                {{ u.displayName }}
                <span class="muted small"
                  >{{ u.email }}{{ u.department ? ' · ' + u.department : '' }}</span
                >
              </label>
            }
          </div>
          <div class="row-inline">
            <label class="inline"
              >Due date
              <input
                type="date"
                [ngModel]="dueDate()"
                (ngModelChange)="dueDate.set($event)"
                name="due"
            /></label>
            <button
              class="btn primary"
              type="button"
              (click)="assign()"
              [disabled]="!selected().size || busy()"
            >
              Assign to {{ selected().size }} {{ selected().size === 1 ? 'person' : 'people' }}
            </button>
          </div>
        </div>
      </section>

      <section class="card flush">
        <h2 class="pad">Learners ({{ report()?.rows?.length ?? 0 }})</h2>
        <table class="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Progress</th>
              <th>Due</th>
              <th>Completed</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (r of report()?.rows ?? []; track r.userId) {
              <tr>
                <td>
                  <a [routerLink]="['/admin/reports/users', r.userId]">{{ r.displayName }}</a>
                  <div class="muted small">{{ r.email }}</div>
                </td>
                <td class="progress-cell">
                  <app-progress [value]="r.percent" /><span class="small muted"
                    >{{ r.completedItems }}/{{ r.totalItems }}</span
                  >
                </td>
                <td class="small" [class.overdue]="r.overdue">
                  {{
                    r.dueDate
                      ? (r.dueDate | date: 'mediumDate')
                      : r.assigned
                        ? '—'
                        : 'Self-enrolled'
                  }}
                </td>
                <td class="small">
                  {{ r.completedAt ? (r.completedAt | date: 'mediumDate') : '' }}
                </td>
                <td class="row-actions">
                  <button
                    class="link danger"
                    type="button"
                    (click)="unassign(r.userId, r.displayName)"
                  >
                    Remove
                  </button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="muted">
                  Nobody has started or been assigned this course yet.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }
  `,
})
export class CourseEdit implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly id = input<string>();

  protected readonly modules = signal<AdminModule[]>([]);
  protected readonly content = signal<AdminContent[]>([]);
  protected readonly users = signal<User[]>([]);
  protected readonly existing = signal<AdminCourse | null>(null);
  protected readonly report = signal<CourseReport | null>(null);
  protected readonly steps = signal<AdminCourseItem[]>([]);
  protected readonly pickerText = signal('');
  protected readonly userFilter = signal('');
  protected readonly selected = signal(new Set<number>());
  protected readonly dueDate = signal('');
  protected readonly ready = signal(false);
  protected readonly busy = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal('');

  protected title = '';
  protected description: string | null = '';
  protected moduleId: number | null = null;
  protected enforceOrder = true;
  protected isPublished = false;

  protected readonly pickerResults = computed(() => {
    const t = this.pickerText().trim().toLowerCase();
    const inCourse = new Set(this.steps().map((s) => s.contentId));
    return this.content()
      .filter(
        (c) =>
          !inCourse.has(c.id) &&
          (c.title.toLowerCase().includes(t) || c.modulePath.toLowerCase().includes(t)),
      )
      .slice(0, 12);
  });

  protected readonly filteredUsers = computed(() => {
    const t = this.userFilter().trim().toLowerCase();
    return this.users().filter(
      (u) =>
        u.isActive &&
        (!t || `${u.displayName} ${u.email} ${u.department ?? ''}`.toLowerCase().includes(t)),
    );
  });

  async ngOnInit(): Promise<void> {
    try {
      // Load the course before filling the step picker, so a step added straight away
      // is not overwritten by the course's saved steps arriving later.
      const [modules, content, users] = await Promise.all([
        this.api.modules(),
        this.api.contentList(),
        this.api.users(),
        this.id() ? this.load(Number(this.id())) : null,
      ]);
      this.modules.set(modules);
      this.content.set(content);
      this.users.set(users);
      this.ready.set(true);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected addStep(c: AdminContent): void {
    this.steps.update((s) => [
      ...s,
      {
        contentId: c.id,
        title: c.title,
        type: c.type,
        isPublished: c.isPublished,
        modulePath: c.modulePath,
      },
    ]);
    this.pickerText.set('');
  }

  protected move(i: number, delta: number): void {
    this.steps.update((s) => {
      const copy = [...s];
      [copy[i], copy[i + delta]] = [copy[i + delta], copy[i]];
      return copy;
    });
  }

  protected removeStep(i: number): void {
    this.steps.update((s) => s.filter((_, j) => j !== i));
  }

  protected toggle(userId: number): void {
    this.selected.update((s) => {
      const copy = new Set(s);
      if (copy.has(userId)) copy.delete(userId);
      else copy.add(userId);
      return copy;
    });
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.saved.set(false);
    this.error.set('');
    try {
      const body = {
        title: this.title,
        description: this.description,
        moduleId: this.moduleId,
        enforceOrder: this.enforceOrder,
        isPublished: this.isPublished,
        contentIds: this.steps().map((s) => s.contentId),
      };
      const current = this.existing();
      if (current) {
        await this.api.updateCourse(current.id, body);
        await this.load(current.id);
        this.saved.set(true);
      } else {
        const created = await this.api.createCourse(body);
        this.router.navigate(['/admin/courses', created.id], { replaceUrl: true });
      }
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async assign(): Promise<void> {
    const c = this.existing();
    if (!c) return;
    this.busy.set(true);
    try {
      // The date input gives a local calendar date; treat the course as due at the end of that day.
      const due = this.dueDate() ? new Date(`${this.dueDate()}T23:59:59`).toISOString() : null;
      await this.api.assign(c.id, [...this.selected()], due);
      this.selected.set(new Set());
      this.report.set(await this.api.courseReport(c.id));
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async unassign(userId: number, name: string): Promise<void> {
    const c = this.existing();
    if (!c || !confirm(`Remove ${name} from this course? Their viewing history is kept.`)) return;
    try {
      await this.api.unassign(c.id, userId);
      this.report.set(await this.api.courseReport(c.id));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected async remove(): Promise<void> {
    const c = this.existing();
    if (
      !c ||
      !confirm(`Delete the course "${c.title}"? Enrollments are removed; viewing history is kept.`)
    )
      return;
    try {
      await this.api.deleteCourse(c.id);
      this.router.navigate(['/admin/courses']);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private async load(id: number): Promise<void> {
    const [c, report] = await Promise.all([this.api.course(id), this.api.courseReport(id)]);
    this.existing.set(c);
    this.report.set(report);
    this.steps.set(c.items);
    this.title = c.title;
    this.description = c.description;
    this.moduleId = c.moduleId;
    this.enforceOrder = c.enforceOrder;
    this.isPublished = c.isPublished;
    this.cdr.markForCheck();
  }
}
